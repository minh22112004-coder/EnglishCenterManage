import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import axios from 'axios';
import { User, Lock, ArrowRight, ShieldCheck } from 'lucide-react';
import { setSession } from '../utils/auth';

const Login = () => {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const navigate = useNavigate();

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError('');

    try {
      // Gọi API đến Backend
      const response = await axios.post(`/auth/login`, {
        username,
        password
      });

      const session = setSession({
        token: response.data.token,
        role: response.data.role,
        roles: response.data.roles,
        user: response.data.user
      });

      // Chuyển hướng theo active portal
      if (session.activePortal === 'Admin') navigate('/admin');
      else if (session.activePortal === 'Lecture') navigate('/lecture');
      else navigate('/user');

    } catch (err: any) {
      if (err.response?.status === 429) {
        setError(err.response?.data?.message || 'Bạn đã thử đăng nhập quá nhiều lần. Vui lòng thử lại sau 1 phút.');
      } else {
        setError(err.response?.data?.message || 'Đăng nhập thất bại. Vui lòng kiểm tra lại Username/Password.');
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-indigo-900 via-purple-900 to-slate-900 flex items-center justify-center p-4 relative overflow-hidden">

      {/* Các hình tròn làm mờ để tạo phong cách Glassmorphism & Rich Aesthetics */}
      <div className="absolute top-[-10%] left-[-10%] w-96 h-96 bg-purple-600 rounded-full mix-blend-multiply filter blur-[128px] opacity-70 animate-pulse"></div>
      <div className="absolute top-[20%] right-[-10%] w-96 h-96 bg-cyan-500 rounded-full mix-blend-multiply filter blur-[128px] opacity-70 animate-pulse" style={{ animationDelay: '2s' }}></div>
      <div className="absolute bottom-[-20%] left-[20%] w-96 h-96 bg-indigo-600 rounded-full mix-blend-multiply filter blur-[128px] opacity-70 animate-pulse" style={{ animationDelay: '4s' }}></div>

      <div className="w-full max-w-md relative z-10">
        <div className="bg-white/10 backdrop-blur-xl border border-white/20 rounded-3xl p-8 shadow-2xl overflow-hidden">
          <div className="flex justify-center mb-8">
            <div className="bg-gradient-to-tr from-cyan-400 to-indigo-500 p-4 rounded-2xl shadow-lg transform -rotate-6 hover:rotate-0 transition-transform duration-300">
              <ShieldCheck className="w-10 h-10 text-white" />
            </div>
          </div>

          <h2 className="text-3xl font-bold text-center text-white mb-2 tracking-tight">Hệ Thống Quản Lý</h2>
          <p className="text-center text-indigo-200 mb-8 text-sm">Đăng nhập để tiếp tục</p>

          {error && (
            <div className="mb-6 p-4 bg-red-500/20 border border-red-500/50 rounded-xl text-red-100 text-sm">
              {error}
            </div>
          )}

          <form onSubmit={handleLogin} className="space-y-6">
            <div className="space-y-1 relative group">
              <label className="text-sm font-medium text-indigo-100 ml-1">Tên đăng nhập</label>
              <div className="relative">
                <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-indigo-300 group-focus-within:text-cyan-400 transition-colors">
                  <User size={20} />
                </div>
                <input
                  type="text"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  className="w-full bg-slate-900/50 border border-white/10 rounded-xl py-3 pl-10 pr-4 text-white placeholder-indigo-300/50 focus:outline-none focus:ring-2 focus:ring-cyan-400/50 focus:border-cyan-400/50 transition-all"
                  placeholder="Nhập tài khoản"
                  required
                />
              </div>
            </div>

            <div className="space-y-1 relative group">
              <label className="text-sm font-medium text-indigo-100 ml-1">Mật khẩu</label>
              <div className="relative">
                <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-indigo-300 group-focus-within:text-cyan-400 transition-colors">
                  <Lock size={20} />
                </div>
                <input
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  className="w-full bg-slate-900/50 border border-white/10 rounded-xl py-3 pl-10 pr-4 text-white placeholder-indigo-300/50 focus:outline-none focus:ring-2 focus:ring-cyan-400/50 focus:border-cyan-400/50 transition-all"
                  placeholder="••••••••"
                  required
                />
              </div>
            </div>

            <button
              type="submit"
              disabled={loading}
              className="w-full bg-gradient-to-r from-cyan-500 to-indigo-600 hover:from-cyan-400 hover:to-indigo-500 text-white font-semibold rounded-xl py-3 px-4 flex items-center justify-center space-x-2 shadow-lg shadow-indigo-500/30 transform hover:-translate-y-0.5 transition-all duration-200 disabled:opacity-70 disabled:cursor-not-allowed disabled:transform-none"
            >
              <span>{loading ? 'Đang xử lý...' : 'Đăng nhập'}</span>
              {!loading && <ArrowRight size={20} className="ml-1" />}
            </button>
          </form>

        </div>

        <p className="text-center text-indigo-300/60 text-xs mt-6">
          &copy; 2026 Management System. All rights reserved.
        </p>
      </div>
    </div>
  );
};

export default Login;
