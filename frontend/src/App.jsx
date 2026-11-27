import { Navigate, Route, Routes } from 'react-router-dom';
import Layout from './components/Layout.jsx';
import ProtectedRoute from './auth/ProtectedRoute.jsx';
import { useAuth } from './auth/AuthContext.jsx';
import { GROUPS, ROLES, homePathFor } from './auth/roles.js';
import Login from './pages/Login.jsx';
import Register from './pages/Register.jsx';
import Forbidden from './pages/Forbidden.jsx';
import NotFound from './pages/NotFound.jsx';
import MyRequests from './pages/citizen/MyRequests.jsx';
import NewRequest from './pages/citizen/NewRequest.jsx';
import PublicShelters from './pages/citizen/PublicShelters.jsx';
import Volunteer from './pages/citizen/Volunteer.jsx';
import RequestDetail from './pages/shared/RequestDetail.jsx';
import StaffHome from './pages/staff/StaffHome.jsx';
import RequestQueue from './pages/staff/RequestQueue.jsx';
import CrisisEvents from './pages/staff/CrisisEvents.jsx';
import Shelters from './pages/staff/Shelters.jsx';
import ShelterForm from './pages/staff/ShelterForm.jsx';
import ShelterDetail from './pages/staff/ShelterDetail.jsx';
import SupplyRequests from './pages/staff/SupplyRequests.jsx';
import Volunteers from './pages/staff/Volunteers.jsx';
import Tasks from './pages/staff/Tasks.jsx';
import TaskForm from './pages/staff/TaskForm.jsx';
import TaskDetail from './pages/staff/TaskDetail.jsx';
import Resources from './pages/staff/Resources.jsx';
import ResourceForm from './pages/staff/ResourceForm.jsx';
import Allocations from './pages/staff/Allocations.jsx';
import Users from './pages/staff/Users.jsx';
import Audit from './pages/staff/Audit.jsx';
import Notifications from './pages/Notifications.jsx';
import OperationsDashboard from './pages/staff/OperationsDashboard.jsx';
import MapPage from './pages/staff/MapPage.jsx';
import Communications from './pages/staff/Communications.jsx';

function RoleRedirect() {
  const { user } = useAuth();
  return <Navigate to={homePathFor(user.role)} replace />;
}

export default function App() {
  return (
    <Routes>
      {/* public */}
      <Route path="/login" element={<Login />} />
      <Route path="/register" element={<Register />} />

      {/* everything below needs a signed-in user */}
      <Route element={<ProtectedRoute />}>
        <Route element={<Layout />}>
          <Route index element={<RoleRedirect />} />
          <Route path="forbidden" element={<Forbidden />} />
          <Route path="notifications" element={<Notifications />} />

          {/* citizens (volunteers are citizen accounts with a volunteer profile) */}
          <Route element={<ProtectedRoute roles={[ROLES.Citizen]} />}>
            <Route path="citizen" element={<MyRequests />} />
            <Route path="citizen/requests/new" element={<NewRequest />} />
            <Route path="citizen/requests/:id" element={<RequestDetail />} />
            <Route path="citizen/shelters" element={<PublicShelters />} />
            <Route path="citizen/volunteer" element={<Volunteer />} />
          </Route>

          {/* staff (every non-citizen role) - each block repeats the API's role list for that endpoint */}
          <Route element={<ProtectedRoute roles={GROUPS.allStaff} />}>
            <Route path="staff" element={<StaffHome />} />

            <Route element={<ProtectedRoute roles={GROUPS.requestStaff} />}>
              <Route path="staff/requests" element={<RequestQueue />} />
              <Route path="staff/requests/:id" element={<RequestDetail />} />
            </Route>

            <Route element={<ProtectedRoute roles={GROUPS.crisisManage} />}>
              <Route path="staff/crises" element={<CrisisEvents />} />
            </Route>

            <Route element={<ProtectedRoute roles={GROUPS.shelterView} />}>
              <Route path="staff/shelters" element={<Shelters />} />
              <Route path="staff/shelters/:id" element={<ShelterDetail />} />
            </Route>
            <Route element={<ProtectedRoute roles={GROUPS.shelterManage} />}>
              <Route path="staff/shelters/new" element={<ShelterForm />} />
            </Route>
            <Route element={<ProtectedRoute roles={GROUPS.supplies} />}>
              <Route path="staff/supply-requests" element={<SupplyRequests />} />
            </Route>

            <Route element={<ProtectedRoute roles={GROUPS.volunteerView} />}>
              <Route path="staff/volunteers" element={<Volunteers />} />
              <Route path="staff/tasks" element={<Tasks />} />
              <Route path="staff/tasks/:id" element={<TaskDetail />} />
            </Route>
            <Route element={<ProtectedRoute roles={GROUPS.volunteerCoordinate} />}>
              <Route path="staff/tasks/new" element={<TaskForm />} />
            </Route>

            <Route element={<ProtectedRoute roles={GROUPS.resourceView} />}>
              <Route path="staff/resources" element={<Resources />} />
              <Route path="staff/allocations" element={<Allocations />} />
            </Route>
            <Route element={<ProtectedRoute roles={GROUPS.resourceManage} />}>
              <Route path="staff/resources/new" element={<ResourceForm />} />
            </Route>

            <Route element={<ProtectedRoute roles={GROUPS.metrics} />}>
              <Route path="staff/operations" element={<OperationsDashboard />} />
            </Route>
            <Route element={<ProtectedRoute roles={GROUPS.mapView} />}>
              <Route path="staff/map" element={<MapPage />} />
            </Route>
            <Route element={<ProtectedRoute roles={GROUPS.communications} />}>
              <Route path="staff/communications" element={<Communications />} />
            </Route>

            <Route element={<ProtectedRoute roles={GROUPS.admin} />}>
              <Route path="staff/users" element={<Users />} />
              <Route path="staff/audit" element={<Audit />} />
            </Route>
          </Route>

          <Route path="*" element={<NotFound />} />
        </Route>
      </Route>
    </Routes>
  );
}
