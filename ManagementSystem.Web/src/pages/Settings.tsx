import React, { useState, useEffect } from 'react';
import { Save, Building, CreditCard, User, Database, Download } from 'lucide-react';
import axios from 'axios';

const Settings = () => {
  const [formData, setFormData] = useState({
    bankId: '',
    accountNo: '',
    accountName: '',
    customQrUrl: ''
  });
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchSettings();
  }, []);

  const fetchSettings = async () => {
    try {
      const res = await axios.get(`/settings/payment`);
      setFormData(res.data);
    } catch (error) {
      console.error('Lỗi tải cài đặt:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      const token = localStorage.getItem('token');
      await axios.post(`/settings/payment`, formData, {
        headers: { Authorization: `Bearer ${token}` }
      });
      alert('Đã lưu thông tin thanh toán thành công!');
    } catch (error) {
      console.error('Lỗi lưu cài đặt:', error);
      alert('Lưu cài đặt thất bại.');
    }
  };

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!e.target.files || e.target.files.length === 0) return;
    const file = e.target.files[0];
    const uploadData = new FormData();
    uploadData.append('file', file);
    
    try {
      const token = localStorage.getItem('token');
      const res = await axios.post(`/settings/upload-qr`, uploadData, {
        headers: { 
          Authorization: `Bearer ${token}`,
          'Content-Type': 'multipart/form-data'
        }
      });
      setFormData(prev => ({ ...prev, customQrUrl: res.data.url }));
      alert('Tải ảnh lên thành công! Hãy nhấn "Lưu thay đổi" để áp dụng.');
    } catch (error) {
      console.error('Lỗi tải ảnh:', error);
      alert('Tải ảnh thất bại.');
    }
  };

  const handleBackup = async () => {
    try {
      const token = localStorage.getItem('token');
      const response = await axios.get(`/settings/backup`, {
        headers: { Authorization: `Bearer ${token}` },
        responseType: 'blob'
      });
      
      const url = window.URL.createObjectURL(new Blob([response.data]));
      const link = document.createElement('a');
      link.href = url;
      const disposition = response.headers['content-disposition'];
      let fileName = 'backup.bak';
      if (disposition && disposition.indexOf('attachment') !== -1) {
          const filenameRegex = /filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/;
          const matches = filenameRegex.exec(disposition);
          if (matches != null && matches[1]) { 
            fileName = matches[1].replace(/['"]/g, '');
          }
      }
      link.setAttribute('download', fileName);
      document.body.appendChild(link);
      link.click();
      link.parentNode?.removeChild(link);
    } catch (error: any) {
      console.error('Lỗi tải backup:', error);
      alert('Không thể tạo bản sao lưu. Vui lòng đảm bảo máy chủ được cài đặt pg_dump và cấu hình đúng.');
    }
  };

  if (loading) return <div className="p-8 text-center text-slate-500">Đang tải cài đặt...</div>;

  return (
    <div className="space-y-6 animate-fade-in-up max-w-3xl">
      <div className="bg-white p-6 rounded-2xl shadow-sm border border-slate-100">
        <h1 className="text-2xl font-bold text-slate-800">Cài đặt Hệ thống</h1>
        <p className="text-slate-500 text-sm mt-1">Cấu hình các thông số chung cho hệ thống</p>
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-slate-100 overflow-hidden">
        <div className="border-b border-slate-100 p-6 bg-slate-50/50">
          <h2 className="text-lg font-bold text-slate-800 flex items-center">
            <CreditCard className="mr-2 text-indigo-500" /> Cấu hình Thanh toán (Mã QR)
          </h2>
          <p className="text-sm text-slate-500 mt-1">Thông tin này sẽ được hiển thị cho Phụ huynh khi thanh toán học phí.</p>
        </div>
        
        <form onSubmit={handleSave} className="p-6 space-y-6">
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-2">Ngân hàng (Mã định danh VietQR)</label>
            <div className="relative">
              <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                <Building size={18} className="text-slate-400" />
              </div>
              <input
                type="text"
                required
                placeholder="VD: vietcombank, mbbank, techcombank..."
                value={formData.bankId}
                onChange={e => setFormData({ ...formData, bankId: e.target.value })}
                className="pl-10 w-full border border-slate-200 rounded-xl px-4 py-2.5 focus:ring-2 focus:ring-indigo-500/50 transition-all"
              />
            </div>
            <p className="text-xs text-slate-400 mt-2">Sử dụng tên rút gọn tiếng Anh của ngân hàng (VD: mbbank, vietcombank, techcombank)</p>
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700 mb-2">Số tài khoản nhận</label>
            <div className="relative">
              <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                <CreditCard size={18} className="text-slate-400" />
              </div>
              <input
                type="text"
                required
                value={formData.accountNo}
                onChange={e => setFormData({ ...formData, accountNo: e.target.value })}
                className="pl-10 w-full border border-slate-200 rounded-xl px-4 py-2.5 focus:ring-2 focus:ring-indigo-500/50 transition-all"
              />
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700 mb-2">Tên chủ tài khoản</label>
            <div className="relative">
              <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                <User size={18} className="text-slate-400" />
              </div>
              <input
                type="text"
                required
                value={formData.accountName}
                onChange={e => setFormData({ ...formData, accountName: e.target.value.toUpperCase() })}
                className="pl-10 w-full border border-slate-200 rounded-xl px-4 py-2.5 focus:ring-2 focus:ring-indigo-500/50 transition-all"
              />
            </div>
          </div>

          <div className="pt-4 border-t border-slate-100">
            <h3 className="text-sm font-medium text-slate-700 mb-4">Hoặc tải lên Ảnh QR tự tạo (Sẽ ưu tiên sử dụng nếu có)</h3>
            <div className="flex items-center space-x-4">
              {formData.customQrUrl && (
                <div className="w-24 h-24 border rounded-xl overflow-hidden flex-shrink-0">
                  <img src={`${import.meta.env.VITE_BASE_URL || 'http://localhost:5146'}${formData.customQrUrl}`} alt="Custom QR" className="w-full h-full object-cover" />
                </div>
              )}
              <div className="flex-1">
                <input 
                  type="file" 
                  accept="image/*"
                  onChange={handleFileUpload}
                  className="block w-full text-sm text-slate-500 file:mr-4 file:py-2 file:px-4 file:rounded-full file:border-0 file:text-sm file:font-semibold file:bg-indigo-50 file:text-indigo-700 hover:file:bg-indigo-100"
                />
                {formData.customQrUrl && (
                  <button 
                    type="button" 
                    onClick={() => setFormData({...formData, customQrUrl: ''})}
                    className="mt-2 text-xs text-red-500 hover:text-red-700 font-medium"
                  >
                    Xóa ảnh này
                  </button>
                )}
              </div>
            </div>
          </div>

          <div className="pt-4 border-t border-slate-100 flex justify-end">
            <button
              type="submit"
              className="flex items-center px-6 py-2.5 bg-indigo-600 hover:bg-indigo-700 text-white font-medium rounded-xl shadow-sm transition-all"
            >
              <Save size={18} className="mr-2" />
              Lưu thay đổi
            </button>
          </div>
        </form>
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-slate-100 overflow-hidden mt-6">
        <div className="border-b border-slate-100 p-6 bg-slate-50/50 flex justify-between items-center">
          <div>
            <h2 className="text-lg font-bold text-slate-800 flex items-center">
              <Database className="mr-2 text-indigo-500" /> Sao lưu Dữ liệu (Backup Database)
            </h2>
            <p className="text-sm text-slate-500 mt-1">Tải về bản sao lưu toàn bộ dữ liệu hiện tại để phòng ngừa rủi ro.</p>
          </div>
          <button 
            onClick={handleBackup}
            className="flex items-center px-4 py-2 bg-emerald-50 text-emerald-600 rounded-xl font-medium hover:bg-emerald-100 border border-emerald-200 transition-colors"
          >
            <Download size={18} className="mr-2" />
            Tải bản Backup
          </button>
        </div>
      </div>
    </div>
  );
};

export default Settings;
