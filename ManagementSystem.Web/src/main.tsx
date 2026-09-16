import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import axios from 'axios'
import { toast } from 'react-hot-toast'
import { clearSession } from './utils/auth'

axios.defaults.baseURL = import.meta.env.VITE_API_URL || 'http://localhost:5146/api';

// Gắn token hiện tại cho mọi request xác thực, kể cả các request chưa truyền headers.
// Không ghi đè Authorization đã được caller chỉ định.
axios.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token');
    const isLoginRequest = config.url?.replace(/\/$/, '').endsWith('/auth/login');

    if (token && !isLoginRequest && !config.headers?.Authorization) {
      config.headers.Authorization = `Bearer ${token}`;
    }

    return config;
  },
  (error) => Promise.reject(error)
);

// Chỉ kết thúc session khi request được đánh dấu là request xác thực chính.
// Các request phụ (ví dụ payment settings/notifications) tự xử lý lỗi cục bộ.
axios.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401 && error.config?.authFailureAction === 'logout') {
      clearSession();
      toast.error('Phiên đăng nhập đã hết hạn hoặc bị thu hồi. Vui lòng đăng nhập lại!');
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
