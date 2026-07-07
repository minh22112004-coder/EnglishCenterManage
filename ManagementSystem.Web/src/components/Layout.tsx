import React, { useState, useEffect } from 'react';
import { useNavigate, useLocation, Outlet } from 'react-router-dom';
import axios from 'axios';
import { toast } from 'react-hot-toast';
import { 
  LayoutDashboard, 
  Users, 
  GraduationCap, 
  Calendar, 
  Settings, 
  LogOut, 
  Menu,
  X,
  Bell,
  FileText,
  Trash2,
  Key
} from 'lucide-react';

const Layout = () => {
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();
  const API_URL = import.meta.env.VITE_API_URL || '';

  const [showPasswordModal, setShowPasswordModal] = useState(false);
  const [passwordData, setPasswordData] = useState({ oldPassword: '', newPassword: '', confirmPassword: '' });

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('role');
    navigate('/login');
  };

  const handleChangePassword = async (e: React.FormEvent) => {
    e.preventDefault();
    if (passwordData.newPassword !== passwordData.confirmPassword) {
      toast.error('Mật khẩu mới không khớp!');
      return;
    }
    try {
      const token = localStorage.getItem('token');
      await axios.post(`${API_URL}/auth/change-password`, {
        oldPassword: passwordData.oldPassword,
        newPassword: passwordData.newPassword
      }, {
        headers: { Authorization: `Bearer ${token}` }
      });
      toast.success('Đổi mật khẩu thành công!');
      setShowPasswordModal(false);
      setPasswordData({ oldPassword: '', newPassword: '', confirmPassword: '' });
    } catch (error: any) {
      toast.error(error.response?.data?.message || 'Lỗi khi đổi mật khẩu.');
    }
  };

  const role = localStorage.getItem('role') || 'User';

  const adminNavItems = [
    { name: 'Dashboard', path: '/admin', icon: LayoutDashboard },
    { name: 'Giáo viên', path: '/admin/lectures', icon: Users },
    { name: 'Học sinh', path: '/admin/students', icon: Users },
    { name: 'Lớp học', path: '/admin/classes', icon: GraduationCap },
    { name: 'Lịch học', path: '/admin/schedule', icon: Calendar },
    { name: 'Báo cáo Điểm danh', path: '/admin/attendance-report', icon: FileText },
    { name: 'Cài đặt', path: '/admin/settings', icon: Settings },
    { name: 'Thùng rác', path: '/admin/students/deleted', icon: Trash2 },
  ];

  const lectureNavItems = [
    { name: 'Lịch dạy', path: '/lecture', icon: Calendar }
  ];

  const userNavItems = [
    { name: 'Học tập', path: '/user', icon: GraduationCap }
  ];

  const [notifications, setNotifications] = useState<any[]>([]);
  const [showNotificationPopup, setShowNotificationPopup] = useState(false);
  const [showBellDropdown, setShowBellDropdown] = useState(false);

  useEffect(() => {
    if (role === 'User') {
      fetchNotifications();
    }
  }, [role]);

  const fetchNotifications = async () => {
    try {
      const token = localStorage.getItem('token');
      if (!token) return;
      const res = await axios.get(`/notification/my`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      setNotifications(res.data);
      
      const unreadPopup = res.data.find((n: any) => !n.isRead && !n.hidePopup);
      if (unreadPopup) {
        setShowNotificationPopup(true);
      }
    } catch (err) {
      console.error(err);
    }
  };

  const markAsRead = async (id: number) => {
    try {
      const token = localStorage.getItem('token');
      await axios.put(`/notification/${id}/read`, {}, {
        headers: { Authorization: `Bearer ${token}` }
      });
      fetchNotifications();
    } catch (err) {}
  };

  const hidePopup = async (id: number) => {
    try {
      const token = localStorage.getItem('token');
      await axios.put(`/notification/${id}/hide`, {}, {
        headers: { Authorization: `Bearer ${token}` }
      });
      setShowNotificationPopup(false);
      fetchNotifications();
    } catch (err) {}
  };

  let navItems = adminNavItems;
  if (role === 'Lecture') navItems = lectureNavItems;
  else if (role === 'User') navItems = userNavItems;

  return (
    <div className="min-h-screen bg-slate-50 flex overflow-hidden font-sans">
      
      {/* Mobile sidebar backdrop */}
      {sidebarOpen && (
        <div 
          className="fixed inset-0 z-20 bg-slate-900/50 backdrop-blur-sm lg:hidden transition-opacity"
          onClick={() => setSidebarOpen(false)}
        />
      )}

      {/* Sidebar */}
      <div className={`fixed inset-y-0 left-0 z-30 w-64 bg-white border-r border-slate-200 transform transition-transform duration-300 ease-in-out lg:translate-x-0 lg:static lg:inset-auto flex flex-col ${sidebarOpen ? 'translate-x-0' : '-translate-x-full'}`}>
        
        <div className="flex items-center justify-between h-16 px-6 bg-gradient-to-r from-indigo-600 to-purple-600 text-white shadow-md z-10">
          <span className="text-xl font-bold tracking-wider">{role === 'Admin' ? 'LMS Admin' : (role === 'Lecture' ? 'Giáo viên' : 'Học viên')}</span>
          <button onClick={() => setSidebarOpen(false)} className="lg:hidden text-indigo-100 hover:text-white">
            <X size={24} />
          </button>
        </div>

        <nav className="flex-1 px-4 py-6 space-y-2 overflow-y-auto">
          {navItems.map((item) => {
            const Icon = item.icon;
            const isActive = location.pathname === item.path || (location.pathname.startsWith(item.path) && item.path !== '/admin' && item.path !== '/admin/students');
            return (
              <button
                key={item.name}
                onClick={() => {
                  navigate(item.path);
                  setSidebarOpen(false);
                }}
                className={`w-full flex items-center px-4 py-3 text-sm font-medium rounded-xl transition-all duration-200 group ${
                  isActive 
                    ? 'bg-indigo-50 text-indigo-700 shadow-sm' 
                    : 'text-slate-600 hover:bg-slate-50 hover:text-indigo-600'
                }`}
              >
                <Icon size={20} className={`mr-3 ${isActive ? 'text-indigo-600' : 'text-slate-400 group-hover:text-indigo-500'}`} />
                {item.name}
              </button>
            );
          })}
          
          <div className="pt-4 mt-4 border-t border-slate-200">
            <button 
              onClick={() => setShowPasswordModal(true)}
              className="w-full flex items-center px-4 py-3 text-sm font-medium text-slate-600 rounded-xl hover:bg-slate-50 transition-all mb-2"
            >
              <Key size={20} className="mr-3 text-slate-500" />
              Đổi mật khẩu
            </button>
            <button 
              onClick={handleLogout}
              className="w-full flex items-center px-4 py-3 text-sm font-medium text-red-600 rounded-xl hover:bg-red-50 border border-transparent hover:border-red-100 transition-all"
            >
              <LogOut size={20} className="mr-3 text-red-500" />
              Đăng xuất
            </button>
          </div>
        </nav>
      </div>

      {/* Main content wrapper */}
      <div className="flex-1 flex flex-col w-0 overflow-hidden">
        
        {/* Header */}
        <header className="h-16 bg-white/80 backdrop-blur-md border-b border-slate-200 flex items-center justify-between px-4 lg:px-8 z-10 shadow-sm sticky top-0">
          <div className="flex items-center">
            <button 
              onClick={() => setSidebarOpen(true)}
              className="lg:hidden text-slate-500 hover:text-indigo-600 p-2 mr-4 rounded-md hover:bg-slate-100"
            >
              <Menu size={24} />
            </button>
            <h2 className="text-lg font-semibold text-slate-800 hidden sm:block">
              {navItems.find(i => location.pathname === i.path || (location.pathname.startsWith(i.path) && i.path !== '/admin'))?.name || 'Trang chủ'}
            </h2>
          </div>
          
          <div className="flex-1 flex justify-end items-center space-x-6">
            
            {role === 'User' && (
              <div className="relative">
                <button 
                  onClick={() => setShowBellDropdown(!showBellDropdown)}
                  className="text-slate-400 hover:text-indigo-600 relative transition-colors"
                >
                  <Bell size={20} />
                  {notifications.some(n => !n.isRead) && (
                    <span className="absolute top-0 right-0 block h-2.5 w-2.5 rounded-full bg-red-500 ring-2 ring-white animate-pulse"></span>
                  )}
                </button>

                {showBellDropdown && (
                  <div className="absolute right-0 mt-2 w-80 bg-white rounded-xl shadow-lg border border-slate-100 overflow-hidden z-50">
                    <div className="p-4 bg-slate-50 border-b border-slate-100 font-semibold text-slate-800">Thông báo</div>
                    <div className="max-h-64 overflow-y-auto">
                      {notifications.length === 0 ? (
                        <div className="p-4 text-center text-sm text-slate-500">Không có thông báo nào.</div>
                      ) : (
                        notifications.map(n => (
                          <div 
                            key={n.id} 
                            onClick={() => !n.isRead && markAsRead(n.id)}
                            className={`p-4 border-b border-slate-50 cursor-pointer hover:bg-slate-50 transition-colors ${!n.isRead ? 'bg-indigo-50/50' : ''}`}
                          >
                            <p className={`text-sm ${!n.isRead ? 'text-slate-800 font-semibold' : 'text-slate-600'}`}>{n.message}</p>
                            <p className="text-xs text-slate-400 mt-1">{new Date(n.createdAt).toLocaleDateString()}</p>
                          </div>
                        ))
                      )}
                    </div>
                  </div>
                )}
              </div>
            )}

            <div className="flex items-center space-x-3 cursor-pointer p-1 pr-3 rounded-full hover:bg-slate-50 border border-transparent hover:border-slate-200 transition-all">
              <div className="w-9 h-9 rounded-full bg-gradient-to-tr from-indigo-500 to-purple-500 text-white flex items-center justify-center font-bold shadow-inner">
                A
              </div>
              <div className="hidden sm:flex flex-col items-start">
                <span className="text-sm font-bold text-slate-700 leading-tight">Tài khoản {role}</span>
                <span className="text-xs text-slate-400 font-medium">Truy cập</span>
              </div>
            </div>
          </div>
        </header>

        {/* Main Content */}
        <main className="flex-1 relative overflow-y-auto bg-slate-50 p-4 lg:p-8">
          <div className="animate-fade-in-up max-w-7xl mx-auto">
             <Outlet />
          </div>
        </main>

        {/* Notification Popup (Only shown once per unread message) */}
        {showNotificationPopup && role === 'User' && (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm animate-fade-in-up">
            <div className="bg-white rounded-2xl shadow-xl w-full max-w-sm flex flex-col overflow-hidden">
              <div className="p-6 bg-red-50 flex items-center space-x-3 border-b border-red-100">
                <Bell size={24} className="text-red-500" />
                <h3 className="text-lg font-bold text-red-800">Nhắc nhở học phí</h3>
              </div>
              <div className="p-6 space-y-4">
                <p className="text-slate-700 font-medium">
                  {notifications.find(n => !n.isRead && !n.hidePopup)?.message}
                </p>
                <div className="flex items-center space-x-2 pt-4">
                  <input 
                    type="checkbox" 
                    id="hidePopup" 
                    className="rounded text-indigo-600 focus:ring-indigo-500 border-slate-300"
                    onChange={(e) => {
                      if (e.target.checked) {
                        const notif = notifications.find(n => !n.isRead && !n.hidePopup);
                        if (notif) hidePopup(notif.id);
                      }
                    }}
                  />
                  <label htmlFor="hidePopup" className="text-sm text-slate-500 select-none">
                    Không hiển thị lại thông báo này
                  </label>
                </div>
              </div>
              <div className="px-6 py-4 bg-slate-50 border-t border-slate-100 flex justify-end">
                <button 
                  onClick={() => setShowNotificationPopup(false)} 
                  className="px-6 py-2 text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 font-medium transition-colors"
                >
                  Đóng
                </button>
              </div>
            </div>
          </div>
        )}
      </div>

      {/* Change Password Modal */}
      {showPasswordModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm animate-fade-in-up">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-sm flex flex-col overflow-hidden">
            <div className="p-6 bg-slate-50 border-b border-slate-100 flex justify-between items-center">
              <h3 className="text-lg font-bold text-slate-800">Đổi mật khẩu</h3>
              <button onClick={() => setShowPasswordModal(false)} className="text-slate-400 hover:text-slate-600">
                <X size={20} />
              </button>
            </div>
            <form onSubmit={handleChangePassword} className="p-6 space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">Mật khẩu cũ</label>
                <input 
                  type="password" 
                  required
                  className="w-full px-4 py-2 border border-slate-200 rounded-xl focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none"
                  value={passwordData.oldPassword}
                  onChange={e => setPasswordData({...passwordData, oldPassword: e.target.value})}
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">Mật khẩu mới</label>
                <input 
                  type="password" 
                  required
                  className="w-full px-4 py-2 border border-slate-200 rounded-xl focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none"
                  value={passwordData.newPassword}
                  onChange={e => setPasswordData({...passwordData, newPassword: e.target.value})}
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">Xác nhận mật khẩu mới</label>
                <input 
                  type="password" 
                  required
                  className="w-full px-4 py-2 border border-slate-200 rounded-xl focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none"
                  value={passwordData.confirmPassword}
                  onChange={e => setPasswordData({...passwordData, confirmPassword: e.target.value})}
                />
              </div>
              <div className="pt-4 flex justify-end space-x-3">
                <button 
                  type="button"
                  onClick={() => setShowPasswordModal(false)} 
                  className="px-4 py-2 text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 font-medium transition-colors"
                >
                  Hủy
                </button>
                <button 
                  type="submit"
                  className="px-4 py-2 text-white bg-indigo-600 rounded-lg hover:bg-indigo-700 font-medium transition-colors"
                >
                  Đổi mật khẩu
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

    </div>
  );
};

export default Layout;
