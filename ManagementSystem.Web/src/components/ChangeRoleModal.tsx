import React, { useState, useEffect } from 'react';
import { X, ArrowLeftRight, ShieldAlert, GraduationCap, Users, Sparkles, AlertTriangle, KeyRound, CheckCircle2, Lock, Unlock } from 'lucide-react';
import axios from 'axios';
import { toast } from 'react-hot-toast';

interface ChangeRoleModalProps {
  isOpen: boolean;
  onClose: () => void;
  user: {
    id: string;
    fullName?: string;
    phoneNumber?: string;
    roles?: string[];
    bankName?: string;
    bankNumber?: string;
    qrCodeImage?: string;
  } | null;
  onSuccess: () => void;
}

interface RoleSummary {
  id: string;
  fullName?: string;
  phoneNumber?: string;
  email?: string;
  currentRoles: string[];
  upcomingSchedulesCount: number;
  childrenCount: number;
  childrenNames: string[];
  isLockedOut: boolean;
}

const ChangeRoleModal: React.FC<ChangeRoleModalProps> = ({ isOpen, onClose, user, onSuccess }) => {
  const [summary, setSummary] = useState<RoleSummary | null>(null);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const [targetRole, setTargetRole] = useState<'Lecture' | 'User' | 'Both'>('Lecture');
  const [bankName, setBankName] = useState('');
  const [bankNumber, setBankNumber] = useState('');
  const [qrCodeImage, setQrCodeImage] = useState('');
  const [resetPassword, setResetPassword] = useState(false);

  useEffect(() => {
    if (isOpen && user?.id) {
      fetchSummary();
      setBankName(user.bankName || '');
      setBankNumber(user.bankNumber || '');
      setQrCodeImage(user.qrCodeImage || '');
      setResetPassword(false);
    } else {
      setSummary(null);
    }
  }, [isOpen, user]);

  const fetchSummary = async () => {
    if (!user?.id) return;
    try {
      setLoading(true);
      const token = localStorage.getItem('token');
      const res = await axios.get(`/user/${user.id}/role-summary`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      setSummary(res.data);

      const roles = res.data.currentRoles || [];
      const isLecture = roles.includes('Lecture');
      const isUser = roles.includes('User');

      // Gợi ý target role hợp lý dựa trên vai trò hiện tại
      if (isLecture && isUser) {
        setTargetRole('Both');
      } else if (isLecture) {
        setTargetRole('User'); // Nếu đang là giáo viên thì gợi ý đổi sang phụ huynh
      } else {
        setTargetRole('Lecture'); // Nếu đang là phụ huynh thì gợi ý đổi sang giáo viên
      }
    } catch (error) {
      console.error('Lỗi khi tải thông tin vai trò:', error);
      toast.error('Không thể tải thông tin vai trò của người dùng.');
    } finally {
      setLoading(false);
    }
  };

  const handleImageUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onloadend = () => {
      setQrCodeImage(reader.result as string);
    };
    reader.readAsDataURL(file);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!user?.id) return;

    try {
      setSubmitting(true);
      const token = localStorage.getItem('token');
      const payload = {
        targetRole,
        bankName: (targetRole === 'Lecture' || targetRole === 'Both') ? bankName : undefined,
        bankNumber: (targetRole === 'Lecture' || targetRole === 'Both') ? bankNumber : undefined,
        qrCodeImage: (targetRole === 'Lecture' || targetRole === 'Both') ? qrCodeImage : undefined,
        resetPasswordToDefault: resetPassword
      };

      const res = await axios.post(`/user/${user.id}/change-role`, payload, {
        headers: { Authorization: `Bearer ${token}` }
      });

      toast.success(res.data?.message || 'Cập nhật vai trò thành công!');
      onSuccess();
      onClose();
    } catch (error: any) {
      console.error('Lỗi đổi vai trò:', error);
      toast.error(error.response?.data?.message || 'Có lỗi xảy ra khi đổi vai trò.');
    } finally {
      setSubmitting(false);
    }
  };

  if (!isOpen || !user) return null;

  const currentRoles = summary?.currentRoles || user.roles || [];
  const isCurrentlyLecture = currentRoles.includes('Lecture');
  const isCurrentlyUser = currentRoles.includes('User');

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm animate-fade-in-up">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg flex flex-col max-h-[90vh] overflow-hidden border border-slate-100">
        
        {/* Modal Header */}
        <div className="p-6 border-b border-slate-100 flex justify-between items-center bg-gradient-to-r from-indigo-50/50 to-purple-50/50">
          <div className="flex items-center space-x-3">
            <div className="w-10 h-10 rounded-xl bg-indigo-600 text-white flex items-center justify-center shadow-md shadow-indigo-100">
              <ArrowLeftRight size={20} />
            </div>
            <div>
              <h3 className="text-lg font-bold text-slate-800">Chỉnh sửa Vai trò (Role)</h3>
              <p className="text-xs text-slate-500">Phân quyền tài khoản Giáo viên và Phụ huynh</p>
            </div>
          </div>
          <button onClick={onClose} className="text-slate-400 hover:text-slate-600 p-1 rounded-lg transition-colors">
            <X size={20} />
          </button>
        </div>

        {/* Modal Body */}
        <div className="p-6 overflow-y-auto space-y-5">
          {/* User Info Card */}
          <div className="bg-slate-50 p-4 rounded-xl border border-slate-200/80 space-y-2">
            <div className="flex justify-between items-start">
              <div>
                <p className="font-bold text-slate-800 text-base">{user.fullName || 'Chưa đặt tên'}</p>
                <p className="text-sm text-slate-500 font-mono mt-0.5">SĐT: {user.phoneNumber || 'N/A'}</p>
              </div>
              <div className="flex flex-wrap gap-1">
                {isCurrentlyLecture && (
                  <span className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-semibold bg-indigo-100 text-indigo-700">
                    Giáo viên
                  </span>
                )}
                {isCurrentlyUser && (
                  <span className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-semibold bg-emerald-100 text-emerald-700">
                    Phụ huynh
                  </span>
                )}
                {summary?.isLockedOut && (
                  <span className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-semibold bg-red-100 text-red-700">
                    <Lock size={12} className="mr-1" /> Đang bị khóa
                  </span>
                )}
              </div>
            </div>

            {/* Thống kê liên quan */}
            {summary && (
              <div className="pt-2 border-t border-slate-200/60 text-xs text-slate-600 flex flex-wrap gap-x-4 gap-y-1">
                {summary.childrenCount > 0 && (
                  <span>👨‍👩‍👧 Đang có <strong>{summary.childrenCount}</strong> con theo học ({summary.childrenNames.join(', ')})</span>
                )}
                {summary.upcomingSchedulesCount > 0 && (
                  <span className="text-amber-600 font-medium">
                    📅 Có <strong>{summary.upcomingSchedulesCount}</strong> ca dạy sắp tới
                  </span>
                )}
              </div>
            )}
          </div>

          {/* Cảnh báo ca dạy tương lai nếu chuyển sang thuần Phụ huynh */}
          {targetRole === 'User' && (summary?.upcomingSchedulesCount ?? 0) > 0 && (
            <div className="p-3 bg-amber-50 border border-amber-200 rounded-xl flex items-start space-x-3 text-amber-800 text-xs animate-shake">
              <AlertTriangle size={18} className="text-amber-600 shrink-0 mt-0.5" />
              <div>
                <p className="font-bold">Lưu ý về lịch dạy tương lai:</p>
                <p className="mt-0.5">
                  Giáo viên này đang có <strong>{summary?.upcomingSchedulesCount} ca dạy</strong> từ hôm nay trở đi. 
                  Lịch sử điểm danh quá khứ vẫn được bảo toàn, nhưng bạn cần vào mục <strong>Lịch học</strong> để phân công lại các ca dạy sắp tới cho giáo viên khác.
                </p>
              </div>
            </div>
          )}

          {/* Thông báo mở khóa tài khoản nếu chuyển sang Giáo viên */}
          {summary?.isLockedOut && (targetRole === 'Lecture' || targetRole === 'Both') && (
            <div className="p-3 bg-emerald-50 border border-emerald-200 rounded-xl flex items-start space-x-3 text-emerald-800 text-xs">
              <Unlock size={18} className="text-emerald-600 shrink-0 mt-0.5" />
              <div>
                <p className="font-bold">Mở khóa tài khoản tự động:</p>
                <p className="mt-0.5">Tài khoản này đang bị tạm khóa. Khi nâng cấp vai trò, hệ thống sẽ <strong>tự động mở khóa</strong> để người dùng có thể đăng nhập bình thường.</p>
              </div>
            </div>
          )}

          {/* Form chọn Vai trò mục tiêu */}
          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label className="block text-sm font-semibold text-slate-700 mb-2">Chọn Vai trò mới:</label>
              
              <div className="grid grid-cols-1 gap-2.5">
                {/* Option 1: Giáo viên */}
                <label 
                  className={`flex items-start p-3.5 rounded-xl border-2 cursor-pointer transition-all ${
                    targetRole === 'Lecture' 
                      ? 'border-indigo-600 bg-indigo-50/40 shadow-sm' 
                      : 'border-slate-200 hover:border-slate-300 hover:bg-slate-50/50'
                  }`}
                >
                  <input
                    type="radio"
                    name="targetRole"
                    value="Lecture"
                    checked={targetRole === 'Lecture'}
                    onChange={() => setTargetRole('Lecture')}
                    className="mt-1 h-4 w-4 text-indigo-600 border-slate-300 focus:ring-indigo-500"
                  />
                  <div className="ml-3 flex-1">
                    <div className="flex items-center space-x-2">
                      <GraduationCap size={16} className="text-indigo-600" />
                      <span className="font-bold text-slate-800 text-sm">Chỉ là Giáo viên (Lecture)</span>
                    </div>
                    <p className="text-xs text-slate-500 mt-1">
                      Cấp quyền truy cập portal giảng dạy, điểm danh lớp học, lịch dạy cá nhân. Gỡ bỏ quyền phụ huynh.
                    </p>
                  </div>
                </label>

                {/* Option 2: Phụ huynh */}
                <label 
                  className={`flex items-start p-3.5 rounded-xl border-2 cursor-pointer transition-all ${
                    targetRole === 'User' 
                      ? 'border-indigo-600 bg-indigo-50/40 shadow-sm' 
                      : 'border-slate-200 hover:border-slate-300 hover:bg-slate-50/50'
                  }`}
                >
                  <input
                    type="radio"
                    name="targetRole"
                    value="User"
                    checked={targetRole === 'User'}
                    onChange={() => setTargetRole('User')}
                    className="mt-1 h-4 w-4 text-indigo-600 border-slate-300 focus:ring-indigo-500"
                  />
                  <div className="ml-3 flex-1">
                    <div className="flex items-center space-x-2">
                      <Users size={16} className="text-emerald-600" />
                      <span className="font-bold text-slate-800 text-sm">Chỉ là Phụ huynh (User)</span>
                    </div>
                    <p className="text-xs text-slate-500 mt-1">
                      Chỉ cho phép theo dõi học tập của con, số buổi còn lại và thông báo. Gỡ bỏ quyền giáo viên.
                    </p>
                  </div>
                </label>

                {/* Option 3: Kiêm nhiệm */}
                <label 
                  className={`flex items-start p-3.5 rounded-xl border-2 cursor-pointer transition-all ${
                    targetRole === 'Both' 
                      ? 'border-purple-600 bg-purple-50/40 shadow-sm' 
                      : 'border-slate-200 hover:border-slate-300 hover:bg-slate-50/50'
                  }`}
                >
                  <input
                    type="radio"
                    name="targetRole"
                    value="Both"
                    checked={targetRole === 'Both'}
                    onChange={() => setTargetRole('Both')}
                    className="mt-1 h-4 w-4 text-purple-600 border-slate-300 focus:ring-purple-500"
                  />
                  <div className="ml-3 flex-1">
                    <div className="flex items-center space-x-2">
                      <Sparkles size={16} className="text-purple-600" />
                      <span className="font-bold text-slate-800 text-sm">Kiêm nhiệm cả hai (Giáo viên & Phụ huynh)</span>
                    </div>
                    <p className="text-xs text-slate-500 mt-1">
                      Dành cho giáo viên vừa giảng dạy tại trung tâm vừa có con theo học. Cho phép truy cập đồng thời cả hai trang.
                    </p>
                  </div>
                </label>
              </div>
            </div>

            {/* Thông tin ngân hàng nếu đích có vai trò Giáo viên */}
            {(targetRole === 'Lecture' || targetRole === 'Both') && (
              <div className="p-4 bg-slate-50 rounded-xl border border-slate-200/80 space-y-3">
                <p className="text-xs font-bold text-slate-700 uppercase tracking-wider flex items-center">
                  <GraduationCap size={14} className="mr-1.5 text-indigo-600" />
                  Thông tin nhận lương giáo viên (Không bắt buộc)
                </p>
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="block text-xs font-medium text-slate-600 mb-1">Tên ngân hàng</label>
                    <input 
                      type="text" 
                      placeholder="VD: Vietcombank" 
                      value={bankName} 
                      onChange={e => setBankName(e.target.value)} 
                      className="w-full text-sm border border-slate-200 rounded-lg px-3 py-1.5 focus:ring-2 focus:ring-indigo-500/50" 
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-slate-600 mb-1">Số tài khoản</label>
                    <input 
                      type="text" 
                      placeholder="VD: 1029384756" 
                      value={bankNumber} 
                      onChange={e => setBankNumber(e.target.value)} 
                      className="w-full text-sm border border-slate-200 rounded-lg px-3 py-1.5 focus:ring-2 focus:ring-indigo-500/50" 
                    />
                  </div>
                </div>
              </div>
            )}

            {/* Checkbox Đặt lại mật khẩu */}
            <div className="pt-2">
              <label className="flex items-start space-x-2.5 cursor-pointer">
                <input
                  type="checkbox"
                  checked={resetPassword}
                  onChange={e => setResetPassword(e.target.checked)}
                  className="mt-0.5 h-4 w-4 rounded border-slate-300 text-indigo-600 focus:ring-indigo-500"
                />
                <span className="text-xs text-slate-600 leading-tight">
                  <strong className="text-slate-800 font-semibold">Đặt lại mật khẩu về mặc định:</strong> <code>{user.phoneNumber || 'SĐT'}@123Aa</code> (Dùng khi người dùng quên mật khẩu cũ hoặc bàn giao tài khoản mới).
                </span>
              </label>
            </div>

            {/* Modal Actions */}
            <div className="pt-4 flex justify-end space-x-3 border-t border-slate-100">
              <button
                type="button"
                onClick={onClose}
                disabled={submitting}
                className="px-4 py-2 text-sm font-medium text-slate-600 hover:text-slate-800 hover:bg-slate-100 rounded-xl transition-colors"
              >
                Hủy bỏ
              </button>
              <button
                type="submit"
                disabled={submitting || loading}
                className="px-5 py-2 text-sm font-bold text-white bg-indigo-600 hover:bg-indigo-700 disabled:bg-indigo-300 rounded-xl shadow-sm transition-colors flex items-center"
              >
                {submitting ? (
                  <>
                    <span className="w-4 h-4 border-2 border-white/40 border-t-white rounded-full animate-spin mr-2"></span>
                    Đang xử lý...
                  </>
                ) : (
                  <>
                    <CheckCircle2 size={16} className="mr-1.5" />
                    Xác nhận đổi vai trò
                  </>
                )}
              </button>
            </div>
          </form>
        </div>

      </div>
    </div>
  );
};

export default ChangeRoleModal;
