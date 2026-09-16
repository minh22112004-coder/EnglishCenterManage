import React, { useState, useEffect, useRef } from 'react';
import { 
  Users, 
  Plus, 
  Edit2, 
  Trash2, 
  Calendar, 
  QrCode, 
  Upload, 
  X, 
  KeyRound, 
  ArrowLeftRight, 
  Search, 
  GraduationCap, 
  Lock, 
  Sparkles,
  ShieldCheck
} from 'lucide-react';
import axios from 'axios';
import { toast } from 'react-hot-toast';
import Pagination from '../components/Pagination';
import ChangeRoleModal from '../components/ChangeRoleModal';

const LectureList = () => {
  // Tab state
  const [activeTab, setActiveTab] = useState<'lectures' | 'parents'>('lectures');

  // Lectures state
  const [lectures, setLectures] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

  // Parents state
  const [parents, setParents] = useState<any[]>([]);
  const [parentLoading, setParentLoading] = useState(false);
  const [hasFetchedParents, setHasFetchedParents] = useState(false);
  const [parentSearch, setParentSearch] = useState('');

  // Modals
  const [showFormModal, setShowFormModal] = useState(false);
  const [showScheduleModal, setShowScheduleModal] = useState(false);
  const [selectedLecture, setSelectedLecture] = useState<any>(null);
  
  // Change Role Modal
  const [showChangeRoleModal, setShowChangeRoleModal] = useState(false);
  const [userForChangeRole, setUserForChangeRole] = useState<any>(null);

  // Lecture Form state
  const [formData, setFormData] = useState({ id: '', fullName: '', phoneNumber: '', bankName: '', bankNumber: '', qrCodeImage: '' });
  const [isEditing, setIsEditing] = useState(false);

  // Pagination for Lectures
  const [currentPage, setCurrentPage] = useState(1);
  const [itemsPerPage, setItemsPerPage] = useState(25);

  // Pagination for Parents
  const [parentPage, setParentPage] = useState(1);
  const [parentItemsPerPage, setParentItemsPerPage] = useState(25);
  const hasFetchedLecturesRef = useRef(false);

  const paginatedLectures = React.useMemo(() => {
    const startIndex = (currentPage - 1) * itemsPerPage;
    return lectures.slice(startIndex, startIndex + itemsPerPage);
  }, [lectures, currentPage, itemsPerPage]);

  const filteredParents = React.useMemo(() => {
    if (!parentSearch.trim()) return parents;
    const term = parentSearch.toLowerCase().trim();
    return parents.filter(p => 
      (p.fullName && p.fullName.toLowerCase().includes(term)) ||
      (p.phoneNumber && p.phoneNumber.toLowerCase().includes(term)) ||
      (p.email && p.email.toLowerCase().includes(term)) ||
      (p.children && p.children.some((c: any) => c.fullName && c.fullName.toLowerCase().includes(term)))
    );
  }, [parents, parentSearch]);

  const paginatedParents = React.useMemo(() => {
    const startIndex = (parentPage - 1) * parentItemsPerPage;
    return filteredParents.slice(startIndex, startIndex + parentItemsPerPage);
  }, [filteredParents, parentPage, parentItemsPerPage]);

  const fetchLectures = async () => {
    try {
      setLoading(true);
      const token = localStorage.getItem('token');
      const response = await axios.get(`/user/lectures`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      setLectures(response.data);
    } catch (error) {
      console.error("Lỗi lấy danh sách giáo viên", error);
      toast.error("Không thể tải danh sách giáo viên.");
    } finally {
      setLoading(false);
    }
  };

  const fetchParents = async () => {
    try {
      setParentLoading(true);
      const token = localStorage.getItem('token');
      const response = await axios.get(`/user/parents`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      setParents(response.data);
      setHasFetchedParents(true);
    } catch (error) {
      console.error("Lỗi lấy danh sách phụ huynh", error);
      toast.error("Không thể tải danh sách phụ huynh.");
    } finally {
      setParentLoading(false);
    }
  };

  useEffect(() => {
    if (hasFetchedLecturesRef.current) return;
    hasFetchedLecturesRef.current = true;
    fetchLectures();
  }, []);

  const handleDelete = async (id: string) => {
    if (!window.confirm("Bạn có chắc chắn muốn xóa giáo viên này không?")) return;
    try {
      const token = localStorage.getItem('token');
      await axios.delete(`/user/lectures/${id}`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      toast.success("Xóa giáo viên thành công.");
      fetchLectures();
    } catch (error) {
      console.error("Lỗi xóa", error);
      toast.error("Không thể xóa giáo viên này.");
    }
  };

  const handleResetPassword = async (user: any) => {
    const defaultPassword = `${user.phoneNumber || 'SĐT'}@123Aa`;
    if (!window.confirm(`Bạn có chắc chắn muốn đặt lại mật khẩu cho "${user.fullName || user.phoneNumber}"?\n\nMật khẩu mới sẽ là: ${defaultPassword}`)) {
      return;
    }

    try {
      const token = localStorage.getItem('token');
      const res = await axios.post(`/user/lectures/${user.id}/reset-password`, {}, {
        headers: { Authorization: `Bearer ${token}` }
      });
      toast.success(res.data?.message || `Đã reset mật khẩu về: ${defaultPassword}`);
    } catch (error: any) {
      console.error("Lỗi reset mật khẩu", error);
      toast.error(error.response?.data?.message || "Không thể reset mật khẩu.");
    }
  };

  const handleImageUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    
    const reader = new FileReader();
    reader.onloadend = () => {
      setFormData(prev => ({ ...prev, qrCodeImage: reader.result as string }));
    };
    reader.readAsDataURL(file);
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      const token = localStorage.getItem('token');
      if (isEditing) {
        await axios.put(`/user/lectures/${formData.id}`, formData, {
          headers: { Authorization: `Bearer ${token}` }
        });
        toast.success("Cập nhật giáo viên thành công!");
      } else {
        await axios.post(`/user/lectures`, formData, {
          headers: { Authorization: `Bearer ${token}` }
        });
        toast.success("Thêm giáo viên thành công!");
      }
      setShowFormModal(false);
      resetForm();
      fetchLectures();
      fetchParents();
    } catch (error: any) {
      console.error("Lỗi lưu giáo viên", error);
      toast.error(error.response?.data?.message || "Có lỗi xảy ra khi lưu giáo viên.");
    }
  };

  const resetForm = () => {
    setFormData({ id: '', fullName: '', phoneNumber: '', bankName: '', bankNumber: '', qrCodeImage: '' });
    setIsEditing(false);
  };

  const openEditModal = (lecture: any) => {
    setFormData({
      id: lecture.id,
      fullName: lecture.fullName || '',
      phoneNumber: lecture.phoneNumber || '',
      bankName: lecture.bankName || '',
      bankNumber: lecture.bankNumber || '',
      qrCodeImage: lecture.qrCodeImage || ''
    });
    setIsEditing(true);
    setShowFormModal(true);
  };

  const openScheduleModal = (lecture: any) => {
    setSelectedLecture(lecture);
    setShowScheduleModal(true);
  };

  const openChangeRoleModal = (user: any, defaultRoles: string[] = []) => {
    setUserForChangeRole({
      id: user.id,
      fullName: user.fullName,
      phoneNumber: user.phoneNumber,
      roles: user.roles || defaultRoles,
      bankName: user.bankName,
      bankNumber: user.bankNumber,
      qrCodeImage: user.qrCodeImage
    });
    setShowChangeRoleModal(true);
  };

  return (
    <div className="space-y-6 animate-fade-in-up">
      {/* Header */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 bg-white p-6 rounded-2xl shadow-sm border border-slate-100">
        <div>
          <h1 className="text-2xl font-bold text-slate-800">Quản lý Giáo viên & Phụ huynh</h1>
          <p className="text-slate-500 text-sm mt-1">Quản lý đội ngũ giảng dạy, danh sách phụ huynh và điều chỉnh phân quyền (Role)</p>
        </div>
        {activeTab === 'lectures' && (
          <button 
            onClick={() => { resetForm(); setShowFormModal(true); }}
            className="w-full sm:w-auto flex items-center justify-center px-4 py-2 bg-indigo-600 text-white rounded-xl font-medium hover:bg-indigo-700 shadow-sm transition-colors"
          >
            <Plus size={18} className="mr-2" />
            Thêm Giáo viên
          </button>
        )}
      </div>

      {/* Tabs Navigation */}
      <div className="flex border-b border-slate-200 gap-2">
        <button
          onClick={() => setActiveTab('lectures')}
          className={`flex items-center px-5 py-3 text-sm font-bold border-b-2 transition-all ${
            activeTab === 'lectures'
              ? 'border-indigo-600 text-indigo-600 bg-indigo-50/40 rounded-t-xl'
              : 'border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300'
          }`}
        >
          <GraduationCap size={18} className="mr-2" />
          Đội ngũ Giáo viên
          <span className={`ml-2 px-2 py-0.5 rounded-full text-xs font-semibold ${
            activeTab === 'lectures' ? 'bg-indigo-100 text-indigo-700' : 'bg-slate-100 text-slate-600'
          }`}>
            {lectures.length}
          </span>
        </button>

        <button
          onClick={() => {
            setActiveTab('parents');
            if (!hasFetchedParents) fetchParents();
          }}
          className={`flex items-center px-5 py-3 text-sm font-bold border-b-2 transition-all ${
            activeTab === 'parents'
              ? 'border-indigo-600 text-indigo-600 bg-indigo-50/40 rounded-t-xl'
              : 'border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300'
          }`}
        >
          <Users size={18} className="mr-2" />
          Tài khoản Phụ huynh
          {hasFetchedParents && (
            <span className={`ml-2 px-2 py-0.5 rounded-full text-xs font-semibold ${
              activeTab === 'parents' ? 'bg-indigo-100 text-indigo-700' : 'bg-slate-100 text-slate-600'
            }`}>
              {parents.length}
            </span>
          )}
        </button>
      </div>

      {/* TAB 1: DANH SÁCH GIÁO VIÊN */}
      {activeTab === 'lectures' && (
        <div className="bg-white rounded-2xl shadow-sm border border-slate-100 overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse min-w-[800px]">
              <thead>
                <tr className="bg-slate-50/80 border-b border-slate-200 text-slate-500 text-xs uppercase tracking-wider">
                  <th className="p-4 font-semibold rounded-tl-2xl">Họ Tên</th>
                  <th className="p-4 font-semibold">Số điện thoại</th>
                  <th className="p-4 font-semibold">Thông tin Ngân hàng</th>
                  <th className="p-4 font-semibold text-center">Lịch dạy hôm nay</th>
                  <th className="p-4 font-semibold text-right rounded-tr-2xl">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 text-sm">
                {paginatedLectures.map((l) => (
                  <tr key={l.id} className="hover:bg-slate-50/80 transition-colors group">
                    <td className="p-4 font-bold text-slate-800">
                      <div className="flex items-center">
                        <div className="w-8 h-8 rounded-full bg-indigo-100 text-indigo-700 flex items-center justify-center font-bold mr-3 uppercase">
                          {l.fullName ? l.fullName.charAt(0) : 'L'}
                        </div>
                        <div>
                          <div>{l.fullName}</div>
                          {l.email && <div className="text-xs text-slate-400 font-normal">{l.email}</div>}
                        </div>
                      </div>
                    </td>
                    <td className="p-4 text-slate-600 font-mono">{l.phoneNumber || 'N/A'}</td>
                    <td className="p-4">
                      {l.bankName || l.bankNumber ? (
                        <div>
                          <p className="font-bold text-slate-700">{l.bankName}</p>
                          <p className="text-slate-500 text-xs font-mono">{l.bankNumber}</p>
                        </div>
                      ) : <span className="text-slate-400 italic">Chưa cập nhật</span>}
                    </td>
                    <td className="p-4 text-center">
                      <button 
                        onClick={() => openScheduleModal(l)}
                        className={`inline-flex items-center px-3 py-1.5 rounded-lg text-xs font-bold transition-all border ${
                          l.todaySchedules?.length > 0 
                            ? 'bg-emerald-50 text-emerald-700 border-emerald-200 hover:bg-emerald-100' 
                            : 'bg-slate-50 text-slate-500 border-slate-200 hover:bg-slate-100'
                        }`}
                      >
                        <Calendar size={14} className="mr-1.5" />
                        {l.todaySchedules?.length > 0 ? `Có ${l.todaySchedules.length} ca dạy` : 'Trống lịch'}
                      </button>
                    </td>
                    <td className="p-4 text-right">
                      <div className="flex items-center justify-end space-x-1 sm:space-x-2">
                        {/* Nút Chỉnh Role */}
                        <button 
                          onClick={() => openChangeRoleModal(l, ['Lecture'])}
                          title="Chỉnh vai trò (Chuyển sang Phụ huynh / Kiêm nhiệm)"
                          className="inline-flex items-center p-1.5 text-purple-600 hover:bg-purple-50 rounded-lg transition-colors border border-purple-200 hover:border-purple-300"
                        >
                          <ArrowLeftRight size={16} />
                        </button>
                        
                        {/* Nút Reset Mật khẩu */}
                        <button 
                          onClick={() => handleResetPassword(l)} 
                          title="Đặt lại mật khẩu mặc định"
                          className="p-1.5 text-slate-400 hover:text-amber-600 hover:bg-amber-50 rounded-lg transition-colors"
                        >
                          <KeyRound size={16} />
                        </button>

                        {/* Nút Edit */}
                        <button onClick={() => openEditModal(l)} title="Chỉnh sửa thông tin" className="p-1.5 text-slate-400 hover:text-indigo-600 hover:bg-indigo-50 rounded-lg transition-colors">
                          <Edit2 size={16} />
                        </button>

                        {/* Nút Xóa */}
                        <button onClick={() => handleDelete(l.id)} title="Xóa giáo viên" className="p-1.5 text-slate-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors">
                          <Trash2 size={16} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
                {lectures.length === 0 && !loading && (
                  <tr>
                    <td colSpan={5} className="p-8 text-center text-slate-500">Chưa có dữ liệu giáo viên.</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
          
          <Pagination 
            totalItems={lectures.length}
            itemsPerPage={itemsPerPage}
            currentPage={currentPage}
            onPageChange={setCurrentPage}
            onItemsPerPageChange={(items) => {
              setItemsPerPage(items);
              setCurrentPage(1);
            }}
          />
        </div>
      )}

      {/* TAB 2: DANH SÁCH PHỤ HUYNH */}
      {activeTab === 'parents' && (
        <div className="space-y-4">
          {/* Thanh tìm kiếm phụ huynh */}
          <div className="bg-white p-4 rounded-2xl shadow-sm border border-slate-100 flex items-center">
            <Search size={20} className="text-slate-400 mr-3 shrink-0" />
            <input
              type="text"
              placeholder="Tìm kiếm phụ huynh theo Họ tên, SĐT, Email hoặc Tên con..."
              value={parentSearch}
              onChange={(e) => {
                setParentSearch(e.target.value);
                setParentPage(1);
              }}
              className="w-full bg-transparent border-none outline-none text-slate-800 placeholder-slate-400 text-sm"
            />
            {parentSearch && (
              <button onClick={() => setParentSearch('')} className="text-slate-400 hover:text-slate-600 p-1">
                <X size={16} />
              </button>
            )}
          </div>

          <div className="bg-white rounded-2xl shadow-sm border border-slate-100 overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse min-w-[850px]">
                <thead>
                  <tr className="bg-slate-50/80 border-b border-slate-200 text-slate-500 text-xs uppercase tracking-wider">
                    <th className="p-4 font-semibold rounded-tl-2xl">Họ Tên Phụ huynh</th>
                    <th className="p-4 font-semibold">Số điện thoại</th>
                    <th className="p-4 font-semibold">Học sinh liên kết</th>
                    <th className="p-4 font-semibold">Vai trò</th>
                    <th className="p-4 font-semibold">Trạng thái</th>
                    <th className="p-4 font-semibold text-right rounded-tr-2xl">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 text-sm">
                  {paginatedParents.map((p) => {
                    const isDual = p.roles?.includes('Lecture') && p.roles?.includes('User');
                    return (
                      <tr key={p.id} className="hover:bg-slate-50/80 transition-colors group">
                        <td className="p-4 font-bold text-slate-800">
                          <div className="flex items-center">
                            <div className="w-8 h-8 rounded-full bg-emerald-100 text-emerald-700 flex items-center justify-center font-bold mr-3 uppercase">
                              {p.fullName ? p.fullName.charAt(0) : 'P'}
                            </div>
                            <div>
                              <div>{p.fullName || 'Chưa cập nhật tên'}</div>
                              {p.email && <div className="text-xs text-slate-400 font-normal">{p.email}</div>}
                            </div>
                          </div>
                        </td>
                        <td className="p-4 text-slate-600 font-mono">{p.phoneNumber || 'N/A'}</td>
                        <td className="p-4">
                          {p.children && p.children.length > 0 ? (
                            <div className="flex flex-wrap gap-1.5 max-w-xs">
                              {p.children.map((c: any) => (
                                <span 
                                  key={c.id} 
                                  className={`inline-flex items-center px-2 py-0.5 rounded-md text-xs border ${
                                    c.isDeleted 
                                      ? 'bg-slate-50 text-slate-400 border-slate-200' 
                                      : 'bg-slate-100 text-slate-700 border-slate-200/60'
                                  }`}
                                >
                                  <strong className={c.isDeleted ? 'text-slate-500 font-medium' : ''}>{c.fullName}</strong>
                                  {c.className && (
                                    <span className={`ml-1 ${c.isDeleted ? 'text-slate-400' : 'text-purple-600'}`}>({c.className})</span>
                                  )}
                                  {c.isDeleted ? (
                                    <span className="ml-1 text-[10px] px-1 py-0.2 rounded bg-amber-50 text-amber-700 border border-amber-200/60 font-semibold">Đã nghỉ</span>
                                  ) : (
                                    <span className="ml-1 text-blue-600 font-semibold">• {c.totalRemainingSessions}b</span>
                                  )}
                                </span>
                              ))}
                            </div>
                          ) : (
                            <span className="text-slate-400 italic text-xs">Chưa liên kết học sinh</span>
                          )}
                        </td>
                        <td className="p-4">
                          {isDual ? (
                            <span className="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold bg-purple-100 text-purple-800">
                              <Sparkles size={12} className="mr-1" /> Kiêm nhiệm
                            </span>
                          ) : (
                            <span className="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-700 border border-emerald-200/60">
                              Phụ huynh
                            </span>
                          )}
                        </td>
                        <td className="p-4">
                          {p.isLockedOut ? (
                            <span className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-bold bg-red-50 text-red-700 border border-red-200">
                              <Lock size={12} className="mr-1" /> Bị khóa
                            </span>
                          ) : (
                            <span className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium bg-emerald-50 text-emerald-700">
                              <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 mr-1.5"></span>
                              Hoạt động
                            </span>
                          )}
                        </td>
                        <td className="p-4 text-right">
                          <div className="flex items-center justify-end space-x-2">
                            {/* Nút Nâng quyền / Đổi vai trò */}
                            <button
                              onClick={() => openChangeRoleModal(p, ['User'])}
                              className="inline-flex items-center px-3 py-1.5 bg-indigo-50 hover:bg-indigo-100 text-indigo-700 border border-indigo-200 rounded-lg text-xs font-bold transition-colors"
                              title="Chuyển thành Giáo viên hoặc Kiêm nhiệm"
                            >
                              <ArrowLeftRight size={14} className="mr-1.5" />
                              Chỉnh Role
                            </button>

                            {/* Nút Reset MK */}
                            <button
                              onClick={() => handleResetPassword(p)}
                              title="Đặt lại mật khẩu mặc định"
                              className="p-1.5 text-slate-400 hover:text-amber-600 hover:bg-amber-50 rounded-lg transition-colors"
                            >
                              <KeyRound size={16} />
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                  {filteredParents.length === 0 && !parentLoading && (
                    <tr>
                      <td colSpan={6} className="p-8 text-center text-slate-500">
                        {parentSearch ? 'Không tìm thấy phụ huynh phù hợp.' : 'Chưa có dữ liệu phụ huynh.'}
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>

            <Pagination
              totalItems={filteredParents.length}
              itemsPerPage={parentItemsPerPage}
              currentPage={parentPage}
              onPageChange={setParentPage}
              onItemsPerPageChange={(items) => {
                setParentItemsPerPage(items);
                setParentPage(1);
              }}
            />
          </div>
        </div>
      )}

      {/* Form Modal (Thêm / Sửa Giáo viên) */}
      {showFormModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm animate-fade-in-up">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg flex flex-col max-h-[90vh]">
            <div className="p-6 border-b border-slate-100 flex justify-between items-center">
              <h3 className="text-xl font-bold text-slate-800">{isEditing ? 'Cập nhật Giáo viên' : 'Thêm Giáo viên mới'}</h3>
              <button onClick={() => setShowFormModal(false)} className="text-slate-400 hover:text-slate-600 font-bold"><X size={20}/></button>
            </div>
            <div className="p-6 overflow-y-auto">
              <form onSubmit={handleSave} className="space-y-4">
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Họ và Tên</label>
                  <input type="text" required value={formData.fullName} onChange={e => setFormData({...formData, fullName: e.target.value})} className="w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Số điện thoại (Dùng làm Tên đăng nhập)</label>
                  <input type="tel" required disabled={isEditing} value={formData.phoneNumber} onChange={e => setFormData({...formData, phoneNumber: e.target.value})} className={`w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50 ${isEditing ? 'bg-slate-100 text-slate-500' : ''}`} />
                  {!isEditing && <p className="text-xs text-slate-500 mt-1">Mật khẩu mặc định sẽ là: [Số điện thoại]@123Aa</p>}
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-medium text-slate-700 mb-1">Tên Ngân hàng</label>
                    <input type="text" placeholder="VD: Vietcombank" value={formData.bankName} onChange={e => setFormData({...formData, bankName: e.target.value})} className="w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50" />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-slate-700 mb-1">Số tài khoản</label>
                    <input type="text" placeholder="VD: 1029384756" value={formData.bankNumber} onChange={e => setFormData({...formData, bankNumber: e.target.value})} className="w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50" />
                  </div>
                </div>

                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-2 flex items-center">
                    <QrCode size={16} className="mr-1 text-indigo-600"/> Mã QR Thanh toán (Tùy chọn)
                  </label>
                  
                  <div className="flex items-center space-x-4">
                    {formData.qrCodeImage ? (
                      <div className="relative border rounded-lg p-2 bg-slate-50 inline-block">
                        <img src={formData.qrCodeImage} alt="QR Code" className="w-24 h-24 object-contain" />
                        <button type="button" onClick={() => setFormData({...formData, qrCodeImage: ''})} className="absolute -top-2 -right-2 bg-red-500 text-white rounded-full p-1 shadow-sm hover:bg-red-600">
                          <X size={12} />
                        </button>
                      </div>
                    ) : (
                      <label className="flex flex-col items-center justify-center w-32 h-32 border-2 border-slate-300 border-dashed rounded-xl cursor-pointer bg-slate-50 hover:bg-slate-100 transition-colors">
                        <div className="flex flex-col items-center justify-center pt-5 pb-6 text-slate-500">
                          <Upload size={24} className="mb-2" />
                          <p className="text-xs font-semibold">Tải ảnh lên</p>
                        </div>
                        <input type="file" accept="image/*" className="hidden" onChange={handleImageUpload} />
                      </label>
                    )}
                  </div>
                </div>

                <div className="flex justify-end pt-4 border-t border-slate-100">
                  <button type="button" onClick={() => setShowFormModal(false)} className="px-4 py-2 text-slate-600 hover:bg-slate-100 rounded-xl font-medium mr-2">Hủy</button>
                  <button type="submit" className="px-6 py-2 bg-indigo-600 text-white font-bold rounded-xl shadow-md hover:bg-indigo-700 transition-colors">{isEditing ? 'Cập nhật' : 'Thêm ngay'}</button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}

      {/* Schedule Modal */}
      {showScheduleModal && selectedLecture && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm animate-fade-in-up">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md flex flex-col">
            <div className="p-6 border-b border-slate-100 flex justify-between items-center bg-indigo-50/50 rounded-t-2xl">
              <div>
                <h3 className="text-xl font-bold text-slate-800">Lịch dạy Hôm nay</h3>
                <p className="text-sm text-slate-600 mt-1">Giáo viên: <span className="font-bold text-indigo-700">{selectedLecture.fullName}</span></p>
              </div>
              <button onClick={() => setShowScheduleModal(false)} className="text-slate-400 hover:text-slate-600 font-bold"><X size={20}/></button>
            </div>
            <div className="p-6 max-h-[60vh] overflow-y-auto">
              {(!selectedLecture.todaySchedules || selectedLecture.todaySchedules.length === 0) ? (
                <div className="text-center py-8">
                  <Calendar size={48} className="mx-auto text-slate-200 mb-3" />
                  <p className="text-slate-500">Giáo viên này không có lịch dạy nào trong ngày hôm nay.</p>
                </div>
              ) : (
                <div className="space-y-4">
                  {selectedLecture.todaySchedules.map((schedule: any, idx: number) => (
                    <div key={idx} className="bg-white border-2 border-indigo-100 rounded-xl p-4 shadow-sm relative overflow-hidden">
                      <div className="absolute top-0 left-0 w-1 h-full bg-indigo-500"></div>
                      <h4 className="font-bold text-slate-800 mb-2 flex items-center">
                        <Calendar size={16} className="text-indigo-600 mr-2"/> Ca dạy #{schedule.scheduleId}
                      </h4>
                      <ul className="space-y-2 mt-3">
                        {schedule.classes?.map((c: any, cidx: number) => (
                          <li key={cidx} className="bg-slate-50 p-2 rounded-lg border border-slate-100">
                            <p className="font-bold text-slate-700 text-sm">{c.className}</p>
                            <p className="text-xs text-slate-500 mt-1 font-medium">{c.timeSlots || 'Chưa thiết lập giờ'}</p>
                          </li>
                        ))}
                      </ul>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* Change Role Modal */}
      <ChangeRoleModal
        isOpen={showChangeRoleModal}
        onClose={() => {
          setShowChangeRoleModal(false);
          setUserForChangeRole(null);
        }}
        user={userForChangeRole}
        onSuccess={() => {
          fetchLectures();
          if (hasFetchedParents) {
            fetchParents();
          }
        }}
      />

    </div>
  );
};

export default LectureList;
