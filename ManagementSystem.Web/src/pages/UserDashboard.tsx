import React, { useState, useEffect } from 'react';
import { BookOpen, Calendar, Clock, CreditCard, QrCode, TrendingUp, AlertTriangle, Users, Copy, CheckCircle2 } from 'lucide-react';
import axios from 'axios';

const UserDashboard = () => {
  const [children, setChildren] = useState<any[]>([]);
  const [selectedChild, setSelectedChild] = useState<any>(null);
  const [upcomingSchedules, setUpcomingSchedules] = useState<any[]>([]);
  
  const [showQR, setShowQR] = useState(false);
  const [isCopied, setIsCopied] = useState(false);
  const [isLoading, setIsLoading] = useState(true);

  const [paymentSettings, setPaymentSettings] = useState({
    bankId: 'vietcombank',
    accountNo: '0123456789',
    accountName: 'TRUNG TAM DAO TAO',
    customQrUrl: ''
  });

  useEffect(() => {
    fetchData();
  }, []);

  const fetchData = async () => {
    try {
      const token = localStorage.getItem('token');
      const headers = { Authorization: `Bearer ${token}` };

      const childrenRes = await axios.get(`/student/mychildren`, { headers });
      setChildren(childrenRes.data);
      if (childrenRes.data.length > 0) {
        setSelectedChild(childrenRes.data[0]);
      }

      const schedulesRes = await axios.get(`/workschedule/mychildren-upcoming`, { headers });
      setUpcomingSchedules(schedulesRes.data);

      const settingsRes = await axios.get(`/settings/payment`);
      if (settingsRes.data) {
        setPaymentSettings(settingsRes.data);
      }
    } catch (error) {
      console.error("Lỗi lấy dữ liệu:", error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleCopy = () => {
    if (!selectedChild) return;
    const text = `Dong hoc phi cho hoc sinh ${selectedChild.fullName}`;
    navigator.clipboard.writeText(text);
    setIsCopied(true);
    setTimeout(() => setIsCopied(false), 2000);
  };

  if (isLoading) return <div className="p-8 text-center text-slate-500">Đang tải dữ liệu...</div>;

  const parentName = localStorage.getItem('fullName') || "Phụ huynh";
  const transferContent = selectedChild ? `Dong hoc phi cho hoc sinh ${selectedChild.fullName}` : "";
  const qrUrl = selectedChild 
    ? (paymentSettings.customQrUrl 
        ? `${import.meta.env.VITE_BASE_URL || 'http://localhost:5146'}${paymentSettings.customQrUrl}` 
        : `https://img.vietqr.io/image/${paymentSettings.bankId}-${paymentSettings.accountNo}-compact2.png?amount=0&addInfo=${transferContent.replace(/ /g, '%20')}&accountName=${paymentSettings.accountName.replace(/ /g, '%20')}`)
    : "";

  return (
    <div className="space-y-8 animate-fade-in-up">
      {/* Header Profile */}
      <div className="bg-gradient-to-r from-cyan-600 to-blue-700 p-8 rounded-3xl text-white shadow-lg relative overflow-hidden">
        <div className="absolute top-0 right-0 -mr-20 -mt-20 w-64 h-64 rounded-full bg-white opacity-10 blur-3xl"></div>
        <div className="absolute bottom-0 left-0 -ml-20 -mb-20 w-40 h-40 rounded-full bg-cyan-300 opacity-20 blur-2xl"></div>
        
        <div className="relative z-10 flex flex-col md:flex-row md:items-center justify-between gap-6">
          <div>
            <h1 className="text-3xl font-bold tracking-tight">Xin chào, {parentName}</h1>
            {selectedChild && (
              <p className="text-cyan-100 mt-2 text-lg">Đây là trang thông tin học tập của em <span className="font-semibold text-white">{selectedChild.fullName}</span></p>
            )}
          </div>
          
          {children.length > 1 && (
            <div className="bg-white/20 backdrop-blur-md px-6 py-3 rounded-2xl border border-white/30 text-center flex items-center">
              <Users className="text-cyan-100 mr-3" />
              <select 
                className="bg-transparent text-white font-bold text-lg outline-none cursor-pointer"
                value={selectedChild?.id || ''}
                onChange={(e) => {
                  const child = children.find(c => c.id === parseInt(e.target.value));
                  if (child) setSelectedChild(child);
                }}
              >
                {children.map(c => (
                  <option key={c.id} value={c.id} className="text-slate-800">{c.fullName}</option>
                ))}
              </select>
            </div>
          )}
        </div>
      </div>

      {!selectedChild ? (
        <div className="bg-white p-8 rounded-3xl text-center text-slate-500">
          Chưa có thông tin học sinh được liên kết.
        </div>
      ) : (
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        
        {/* Cột trái: Thống kê học tập */}
        <div className="lg:col-span-2 space-y-8">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            {/* Card Số buổi */}
            <div className={`p-6 rounded-3xl shadow-sm border ${selectedChild.totalRemainingSessions < 3 ? 'bg-red-50 border-red-200' : 'bg-white border-slate-100'}`}>
              <div className="flex items-center justify-between mb-4">
                <div className={`p-3 rounded-2xl ${selectedChild.totalRemainingSessions < 3 ? 'bg-red-100 text-red-600' : 'bg-blue-100 text-blue-600'}`}>
                  <BookOpen size={24} />
                </div>
                {selectedChild.totalRemainingSessions < 3 && (
                  <span className="flex items-center text-sm font-bold text-red-600 bg-red-100 px-3 py-1 rounded-full">
                    <AlertTriangle size={14} className="mr-1" /> Sắp hết
                  </span>
                )}
              </div>
              <h3 className="text-slate-500 font-medium">Số buổi còn lại</h3>
              <div className="mt-2 flex items-baseline">
                <p className={`text-5xl font-extrabold ${selectedChild.totalRemainingSessions < 3 ? 'text-red-600' : 'text-slate-800'}`}>
                  {selectedChild.totalRemainingSessions}
                </p>
                <p className="text-slate-400 font-medium ml-2">buổi</p>
              </div>
              
              {selectedChild.totalRemainingSessions < 3 && (
                <div className="mt-4 p-3 bg-red-100 rounded-xl border border-red-200">
                  <p className="text-sm text-red-700 font-medium">
                    Tài khoản học của em sắp hết. Vui lòng đóng thêm học phí để không gián đoạn quá trình học.
                  </p>
                </div>
              )}
            </div>

            {/* Card Tỷ lệ chuyên cần */}
            <div className="bg-white p-6 rounded-3xl shadow-sm border border-slate-100">
              <div className="flex items-center justify-between mb-4">
                <div className="p-3 rounded-2xl bg-emerald-100 text-emerald-600">
                  <TrendingUp size={24} />
                </div>
              </div>
              <h3 className="text-slate-500 font-medium">Tỷ lệ chuyên cần</h3>
              <div className="mt-2">
                <p className="text-5xl font-extrabold text-slate-800">{selectedChild.attendanceRate}%</p>
              </div>
              <div className="mt-4 w-full bg-slate-100 rounded-full h-2">
                <div className="bg-emerald-500 h-2 rounded-full" style={{ width: `${selectedChild.attendanceRate}%` }}></div>
              </div>
            </div>
          </div>

          {/* Lịch học sắp tới */}
          <div className="bg-white rounded-3xl shadow-sm border border-slate-100 p-6">
            <h2 className="text-xl font-bold text-slate-800 mb-6 flex items-center">
              <Calendar className="mr-2 text-indigo-500" />
              Lịch học sắp tới của {selectedChild.fullName}
            </h2>
            
            {upcomingSchedules.length === 0 ? (
              <p className="text-slate-500">Chưa có lịch học nào sắp tới.</p>
            ) : (
              <div className="space-y-4">
                {upcomingSchedules.map((schedule, idx) => (
                  <div key={idx} className="flex items-center p-5 bg-indigo-50/50 rounded-2xl border border-indigo-100">
                    <div className="bg-white p-4 rounded-xl shadow-sm border border-indigo-50 mr-6 text-center min-w-[100px]">
                      <span className="block text-indigo-600 font-bold text-lg">
                        {new Date(schedule.date).toLocaleDateString('vi-VN')}
                      </span>
                    </div>
                    <div className="flex-1">
                      <h3 className="text-lg font-bold text-slate-800">
                        {schedule.classes.map((c: any) => c.className).join(', ')}
                      </h3>
                      <div className="text-slate-500 mt-2 flex flex-wrap gap-4">
                        <span className="flex items-center">
                          <Clock size={16} className="mr-1" /> {schedule.classes.map((c: any) => c.timeSlots).join(', ')}
                        </span>
                        {schedule.roomCode && (
                          <span className="flex items-center font-medium text-indigo-600">
                            📍 Phòng: {schedule.roomCode}
                          </span>
                        )}
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>

        {/* Cột phải: Thanh toán học phí */}
        <div className="space-y-6">
          <div className="bg-white rounded-3xl shadow-sm border border-slate-100 overflow-hidden sticky top-24">
            <div className="p-6 bg-slate-800 text-white text-center">
              <CreditCard size={32} className="mx-auto text-cyan-400 mb-2" />
              <h2 className="text-xl font-bold">Đóng học phí</h2>
              <p className="text-slate-400 text-sm mt-1">Nhanh chóng & Tiện lợi</p>
            </div>
            
            <div className="p-6 space-y-6">
              
              <div className="bg-cyan-50 p-4 rounded-2xl border border-cyan-100">
                <label className="block text-sm font-medium text-slate-700 mb-2">Nội dung chuyển khoản</label>
                <div className="flex items-center gap-2">
                  <code className="flex-1 bg-white p-3 rounded-xl border border-cyan-200 text-slate-800 font-semibold break-all text-sm">
                    {transferContent}
                  </code>
                  <button 
                    onClick={handleCopy}
                    className="p-3 bg-cyan-600 hover:bg-cyan-700 text-white rounded-xl shadow-sm transition-all"
                    title="Copy nội dung"
                  >
                    {isCopied ? <CheckCircle2 size={20} /> : <Copy size={20} />}
                  </button>
                </div>
                <p className="text-xs text-cyan-600 mt-2 flex items-center">
                  * Vui lòng copy đúng nội dung này để trung tâm cập nhật học phí tự động.
                </p>
              </div>

              {!showQR ? (
                <button 
                  onClick={() => setShowQR(true)}
                  className="w-full bg-cyan-600 hover:bg-cyan-700 text-white py-3 rounded-xl font-bold shadow-md hover:shadow-lg transition-all flex justify-center items-center"
                >
                  <QrCode className="mr-2" /> Tạo mã QR Thanh toán
                </button>
              ) : (
                <div className="text-center bg-slate-50 p-6 rounded-2xl border border-slate-200 animate-fade-in-up">
                  <h3 className="font-bold text-slate-800 mb-4">Quét mã để thanh toán</h3>
                  <div className="bg-white p-3 rounded-2xl shadow-sm inline-block">
                    <img src={qrUrl} alt="QR Code Thanh Toan" className="w-48 h-48 object-contain" />
                  </div>
                  <div className="mt-4 text-sm text-slate-600">
                    <p>Ngân hàng: <span className="font-bold text-slate-800 uppercase">{paymentSettings.bankId}</span></p>
                    <p>Tài khoản: <span className="font-bold text-slate-800">{paymentSettings.accountNo}</span></p>
                    <p>Chủ TK: <span className="font-bold text-slate-800 uppercase">{paymentSettings.accountName}</span></p>
                  </div>
                  <button 
                    onClick={() => setShowQR(false)}
                    className="mt-6 text-sm text-slate-500 hover:text-slate-700 underline"
                  >
                    Đóng mã QR
                  </button>
                </div>
              )}
            </div>
          </div>
        </div>

      </div>
      )}
    </div>
  );
};

export default UserDashboard;
