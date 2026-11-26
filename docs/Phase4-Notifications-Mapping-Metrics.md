# Phase 4: notifications, mapping and operational metrics

This phase adds the three things the brief asks for that were still missing: a communication integration (section I and 3.2),
a mapping integration (section J and 3.3) and operational metrics (advanced requirement 9). It also gives the project its
asynchronous / event-driven workflow (advanced requirement 8) and the "no duplicate notification" rule (requirement 6).

## 1. Notifications

### Flow (event-driven, asynchronous)
```
Request / shelter / volunteer service            publishes a domain event (what happened)
        |                                          e.g. RequestStatusChanged, ShelterPlacementMade, VolunteerAssigned
        v
InProcessDomainEventPublisher                    calls every subscribed handler (Observer / pub-sub)
        v
NotificationEventHandler -> NotificationService  stages a Notification (inbox) + one NotificationDelivery per channel
        |                                          (e-mail, SMS) in the SAME unit of work: the transactional outbox
        v  (one SaveChanges: business change + notification commit together, or neither does)
Database
        ^
NotificationWorker (BackgroundService, every 10 s) -> NotificationDispatchService.ProcessDueAsync
        |   claims due messages, sends them through INotificationChannel (Strategy, chosen by a Factory),
        |   marks Sent / schedules a retry with exponential back-off / parks as Failed
        v
EmailChannel (SMTP, or log-only when no server is configured) | SimulatedSmsChannel
```
A person approving a request never waits for an e-mail server, and a broken e-mail server can never fail or roll back a
humanitarian decision. The notification is saved in the same transaction as the decision, so it can not be lost either.

### Rules that answer the brief
| Requirement | How it is met |
|---|---|
| No duplicate notifications when an event is processed twice (7.6) | Every notification has a dedupe key built from the event id. A unique index on (recipient, dedupe key) is the database backstop, and the service checks first. The dispatcher claims a message (status Processing + lease + row-version) before sending, so two workers never send the same message. |
| Temporary failure of a communication service (7.7, 2.4) | A failed send is retried with exponential back-off (30 s, 1 min, 2 min ... capped at 30 min), at most 5 attempts, then parked as Failed. An administrator can retry it from the API. A malformed address is a permanent error and is not retried. |
| Failure recovery for asynchronous processing (2.4) | A worker that crashes after claiming a message leaves a lease; when it expires another worker picks the message up (at-least-once delivery: in the rare crash-after-send case a person may receive it twice). |
| Multilingual communication (I, A) | Text exists in English, Sinhala and Tamil and is chosen from the person's preferred language, with English as the fallback. The recipient's language is stored with the notification. |
| Application-status, shelter and assignment notifications, emergency announcements (I) | Request received / reviewed / escalated / merged / closed; shelter placed; volunteer task assigned; coordinator announcements to everyone who asked for help in a crisis (or to all citizens). |
| SMS (I) | A second channel built on the same Strategy/Factory. It is a **simulation** (it logs the message; provider "sms-simulated"): no SMS gateway credentials exist. A real gateway would replace one class. Only urgent types use SMS. |
| Privacy | The operations screen masks e-mail addresses and phone numbers; the audit log records announcements; people only see their own notifications (another person's id returns 404). |

### API
| Method and path | Roles | Purpose |
|---|---|---|
| GET `/api/notifications/mine?unreadOnly&page&pageSize` | any signed-in user | My notifications, newest first, with the unread count |
| GET `/api/notifications/unread-count` | any signed-in user | For the bell icon |
| POST `/api/notifications/{id}/read`, POST `/api/notifications/read-all` | any signed-in user | Mark as read |
| POST `/api/notifications/announcements` | EmergencyCoordinator, SystemAdministrator | Emergency announcement `{crisisEventId?, title, message}` |
| GET `/api/notifications/deliveries?status&take` | EmergencyCoordinator, SystemAdministrator | The outbox, for monitoring failures |
| POST `/api/notifications/deliveries/{id}/retry` | EmergencyCoordinator, SystemAdministrator | Put a failed message back in the queue |

### Configuration (`appsettings.json`)
```json
"Notifications": { "PollSeconds": 10 },
"Email": { "Host": "", "Port": 587, "EnableSsl": true, "Username": "", "Password": "",
           "FromAddress": "no-reply@humanitarian.local", "FromName": "Humanitarian Platform" }
```
With `Host` empty the e-mail is written to the API log and recorded as provider `log`, so the whole workflow can be shown
without a mail server. To send real e-mail set the host (for example a Mailtrap or smtp4dev inbox for the demo) and keep the
password out of source control:
```powershell
dotnet user-secrets init --project src\Humanitarian.Api
dotnet user-secrets set "Email:Host" "smtp.example.org" --project src\Humanitarian.Api
dotnet user-secrets set "Email:Username" "user" --project src\Humanitarian.Api
dotnet user-secrets set "Email:Password" "secret" --project src\Humanitarian.Api
```

## 2. Mapping

| Capability | Where |
|---|---|
| Geocoding (address to coordinates) and reverse geocoding | `GET /api/maps/geocode?address=`, `GET /api/maps/reverse?lat=&lng=` (OpenStreetMap Nominatim) |
| Distance and route calculation | `GET /api/maps/route?fromLat&fromLng&toLat&toLng` (OSRM driving routes) |
| Location capture for a request | When a request has an address but no coordinates, the address is geocoded during submission |
| Location-aware matching | Existing shelter, resource and volunteer matchers use the coordinates (now filled in automatically) |
| Geographical reporting and service coverage | `coverage` in the metrics: share of open located requests within 10 km of an open shelter, and the average distance |
| Interactive maps | Web client: shelters map and operations map (Leaflet + OpenStreetMap tiles) |

Resilience (7.7): every external call has a 4 s timeout and one retry (`ResilientCall`). If the provider is unavailable:
geocoding returns a saved earlier answer (`GeocodeCacheEntry`) or says "enter the coordinates manually" and a request is
**still accepted** without coordinates (the history records that the location could not be worked out); routing returns a
straight-line distance with a road factor, clearly labelled as an estimate. Set `Mapping:Enabled` to `false` to demonstrate
the fallbacks. The endpoints have a stricter rate limit (30 per minute) to protect the provider.

Privacy and limitations: only the address text is sent to the provider (no names). Nominatim's public server allows light use
only (identify the application, at most one request per second) and the OSRM demo server has no service guarantee; a real
deployment would host its own or use a commercial provider by writing one adapter class.

## 3. Operational metrics (`GET /api/metrics/operational?crisisEventId=`)
Roles: CaseManager, EmergencyCoordinator, SystemAdministrator. Calculated on demand from the live tables.

| Metric | Definition |
|---|---|
| Open / unresolved / overdue / escalated requests | Open = submitted, under review, triaged or escalated. **Unresolved** = open and nobody has assigned help yet (no resource allocation and no shelter placement). **Overdue** = unresolved for longer than the SLA for its urgency (Critical 2 h, High 6 h, Medium 24 h, Low 72 h). |
| Average response time | Minutes from submission to the first human decision on the AI recommendation; plus average hours to resolution for closed requests |
| AI override frequency | (Modified + Rejected) / decided recommendations; also approved, deferred, fallback rate (AI service unavailable) and average confidence |
| Resource utilisation | Per category: items, total, available, percent in use, low-stock items |
| Shelter occupancy | Capacity, occupancy and percent across open shelters, and how many are full |
| Volunteers | Verified, tasks by state, acceptance rate, hours given |
| Coverage | See mapping |
| Message delivery health | Pending, sent and failed messages |
| Workload | Decisions and overrides per staff member |

Scalability note: the figures are computed in memory per request. For large data volumes they would move to SQL aggregates or a
read model refreshed by the same domain events.

## 4. Design patterns used (for the report)
| Pattern | Where |
|---|---|
| Observer / publish-subscribe | `IDomainEventPublisher`, `IDomainEventHandler<T>`, `NotificationEventHandler` |
| Strategy | `INotificationChannel` (e-mail, SMS), `IGeocodingProvider`, `IRoutingProvider` |
| Factory | `INotificationChannelFactory` resolves the channel for a delivery |
| Adapter | `NominatimGeocodingProvider`, `OsrmRoutingProvider`, `EmailChannel` wrap external services behind application ports |
| Transactional outbox | `Notification` + `NotificationDelivery` saved with the business change |
| Retry with exponential back-off, timeout, fallback | `ResilientCall`, `NotificationDispatchService`, `MappingService` |
| Null object | `NullDomainEventPublisher` |
| Dependency injection / repository / unit of work | Unchanged: new services depend on interfaces; modules register themselves |

## 5. Database changes
New tables: `Notifications`, `NotificationDeliveries` and `GeocodeCacheEntries`. Create the migration with
```powershell
dotnet ef migrations add AddNotificationsAndMapping -p src\Humanitarian.Infrastructure -s src\Humanitarian.Api
```

## 6. Demonstration scenario (UAT: "notify affected user")
1. Citizen (Sinhala as preferred language) submits a request with an address: the address is geocoded and the citizen receives
   "request received" in the inbox; the API log shows the e-mail.
2. Case manager approves the recommendation: "request reviewed" arrives.
3. Coordinator places the household in a shelter: "shelter place arranged" (e-mail and simulated SMS).
4. Stop the mail server (or set a wrong SMTP host): the next notification shows as pending with an error in
   Notifications > Deliveries, and is retried automatically; after the server is back it is sent. Nothing in the main workflow failed.
5. Approve nothing twice: repeating an event does not create a second notification (covered by tests).
6. Set `Mapping:Enabled` to `false`: geocoding says to enter coordinates, a request is still accepted, and a route shows as an estimate.
7. Open the operations dashboard: unresolved/overdue requests, average response time, AI override rate, shelter occupancy.

## 7. Known limitations
- The Sinhala and Tamil texts were drafted with machine assistance and should be reviewed by a native speaker before real use.
- SMS is simulated; push notifications are not implemented.
- Delivery is at-least-once (see above); a message is never lost, but in a crash-after-send case it can repeat.
- Announcements are written in one language (the coordinator's); only system messages are translated.
