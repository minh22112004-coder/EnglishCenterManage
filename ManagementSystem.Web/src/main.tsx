import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import axios from 'axios'
import { toast } from 'react-hot-toast'
import { clearSession } from './utils/auth'

axios.defaults.baseURL = import.meta.env.VITE_API_URL || 'http://localhost:5146/api';

// Tự động đăng xuất sau 3 tiếng (khi Token hết hạn hoặc bị thu hồi và server trả về lỗi 401)
axios.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
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
