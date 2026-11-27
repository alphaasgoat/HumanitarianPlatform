// The audit filter on the API matches the exact action / entity name, so the screen offers the names the API writes.
export const AUDIT_ACTIONS = [
  'LoginSucceeded', 'LoginFailed', 'LoginBlockedLockout', 'UserRegistered', 'StaffCreated', 'UserActivated', 'UserDeactivated',
  'CrisisCreated', 'CrisisStatusChanged', 'AreaAdded',
  'RequestSubmitted', 'AiRecommendationGenerated', 'DuplicatesFlagged', 'DuplicateDismissed', 'RequestMerged', 'RequestEscalated', 'RequestClosed',
  'ResourceCreated', 'ResourceRestocked', 'ResourceActivated', 'ResourceDeactivated', 'ResourceAllocated', 'ResourceDispatched',
  'ResourceDelivered', 'ResourceAllocationCancelled', 'ShortagePlanApplied',
  'ShelterCreated', 'ShelterUpdated', 'ShelterStatusChanged', 'ShelterAllocated', 'ShelterReleased', 'ShelterSupplyRequested', 'ShelterSupplyFulfilled',
  'VolunteerRegistered', 'VolunteerUpdated', 'VolunteerVerified', 'VolunteerActivated', 'VolunteerDeactivated',
  'VolunteerTaskCreated', 'VolunteerTaskCancelled', 'VolunteerAssigned',
  'AssignmentAccepted', 'AssignmentDeclined', 'AssignmentCompleted', 'AssignmentCancelled'
];

export const AUDIT_ENTITIES = [
  'User', 'CrisisEvent', 'OperationalArea', 'AssistanceRequest', 'ResourceItem', 'ResourceAllocation',
  'Shelter', 'ShelterAllocation', 'ShelterSupplyRequest', 'Volunteer', 'VolunteerTask', 'TaskAssignment'
];

/** Which colour family an action belongs to, so the log can be scanned quickly. */
export function auditTone(action) {
  if (/Failed|Blocked|Cancel|Declined|Deactivat|Closed/.test(action)) return 'warn';
  if (/Created|Registered|Submitted|Allocated|Assigned|Verified|Delivered|Succeeded|Fulfilled|Applied/.test(action)) return 'ok';
  return 'info';
}
