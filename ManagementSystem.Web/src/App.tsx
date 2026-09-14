import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import Login from './pages/Login';
import Layout from './components/Layout';
import LectureDashboard from './pages/LectureDashboard';
import UserDashboard from './pages/UserDashboard';
import StudentList from './pages/StudentList';
import DeletedStudents from './pages/DeletedStudents';
import ClassList from './pages/ClassList';
import ScheduleList from './pages/ScheduleList';
import LectureList from './pages/LectureList';
import Settings from './pages/Settings';
import AdminDashboard from './pages/AdminDashboard';
import AttendanceReport from './pages/AttendanceReport';
import { Toaster } from 'react-hot-toast';


// Hàm phân quyền
const ProtectedRoute = ({ children, allowedRoles }: { children: React.ReactNode, allowedRoles: string[] }) => {
  const role = localStorage.getItem('role');
  const rolesJson = localStorage.getItem('roles');
  let roles: string[] = [];
  try {
    roles = rolesJson ? JSON.parse(rolesJson) : (role ? [role] : []);
  } catch {
    roles = role ? [role] : [];
  }

  if (roles.length === 0) {
    return <Navigate to="/login" replace />;
  }

  // Kiểm tra MẢNG roles đầy đủ thay vì chỉ primaryRole
  const hasAccess = allowedRoles.some(r => roles.includes(r));

  if (!hasAccess) {
    // Nếu không có quyền, điều hướng fallback theo vai trò người dùng sở hữu
    if (roles.includes('Admin')) return <Navigate to="/admin" replace />;
    if (roles.includes('Lecture')) return <Navigate to="/lecture" replace />;
    if (roles.includes('User')) return <Navigate to="/user" replace />;
    return <Navigate to="/login" replace />;
  }

  return <>{children}</>;
};

function App() {
  return (
    <>
      <Toaster position="top-right" />
      <Router>
        <Routes>
          <Route path="/login" element={<Login />} />

          {/* Protected routes wrapped in Layout */}
          <Route element={<Layout />}>
            {/* Admin Routes */}
            <Route path="/admin" element={
              <ProtectedRoute allowedRoles={['Admin']}>
                <AdminDashboard />
              </ProtectedRoute>
            } />
            <Route path="/admin/lectures" element={
              <ProtectedRoute allowedRoles={['Admin']}>
                <LectureList />
              </ProtectedRoute>
            } />
            <Route path="/admin/students" element={
              <ProtectedRoute allowedRoles={['Admin']}>
                <StudentList />
              </ProtectedRoute>
            } />
            <Route path="/admin/students/deleted" element={
              <ProtectedRoute allowedRoles={['Admin']}>
                <DeletedStudents />
              </ProtectedRoute>
            } />
            <Route path="/admin/classes" element={
              <ProtectedRoute allowedRoles={['Admin']}>
                <ClassList />
              </ProtectedRoute>
            } />
            <Route path="/admin/schedule" element={
              <ProtectedRoute allowedRoles={['Admin']}>
                <ScheduleList />
              </ProtectedRoute>
            } />
            <Route path="/admin/attendance-report" element={
              <ProtectedRoute allowedRoles={['Admin']}>
                <AttendanceReport />
              </ProtectedRoute>
            } />
            <Route path="/admin/settings" element={
              <ProtectedRoute allowedRoles={['Admin']}>
                <Settings />
              </ProtectedRoute>
            } />

            {/* Lecture Route */}
            <Route path="/lecture" element={
              <ProtectedRoute allowedRoles={['Lecture']}>
                <LectureDashboard />
              </ProtectedRoute>
            } />

            {/* User Route */}
            <Route path="/user" element={
              <ProtectedRoute allowedRoles={['User']}>
                <UserDashboard />
              </ProtectedRoute>
            } />
          </Route>

          {/* Default route */}
          <Route path="*" element={<Navigate to="/login" replace />} />
        </Routes>
      </Router>
    </>
  );
}

export default App;
