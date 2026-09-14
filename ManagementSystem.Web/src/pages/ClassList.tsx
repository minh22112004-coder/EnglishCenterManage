import React, { useState, useEffect } from 'react';
import { Search, Plus, MoreVertical, Edit2, Trash2, Users, History } from 'lucide-react';
import axios from 'axios';

const ClassList = () => {
  const [classes, setClasses] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [attendances, setAttendances] = useState<any[]>([]);
  const [showHistoryModal, setShowHistoryModal] = useState(false);
  const [showAddModal, setShowAddModal] = useState(false);
  const [selectedClassName, setSelectedClassName] = useState('');
  
  const [students, setStudents] = useState<any[]>([]);
  const [searchStudent, setSearchStudent] = useState('');
  
  const [formData, setFormData] = useState<{classCode: string, className: string, timeSlots: string, studentIds: number[]}>({ 
    classCode: '', className: '', timeSlots: '', studentIds: [] 
  });
  const [editId, setEditId] = useState<number | null>(null);

  const resetForm = () => {
    setFormData({ classCode: '', className: '', timeSlots: '', studentIds: [] });
    setEditId(null);
    setSearchStudent('');
  };

  const fetchData = async () => {
    try {
      const token = localStorage.getItem('token');
      const [clsRes, stuRes] = await Promise.all([
        axios.get(`/class`, { headers: { Authorization: `Bearer ${token}` } }),
        axios.get(`/student`, { headers: { Authorization: `Bearer ${token}` } })
      ]);
      setClasses(clsRes.data);
      setStudents(stuRes.data);
    } catch (error) {
      console.error("Lỗi lấy dữ liệu", error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, []);

  const handleDelete = async (id: number) => {
    if (!window.confirm("Bạn có chắc chắn muốn xóa lớp học này không?")) return;
    try {
      const token = localStorage.getItem('token');
      await axios.delete(`/class/${id}`, {
        headers: { Authorization: `Bearer ${token}` }
      });
        fetchData();
      } catch (error) {
        console.error("Lỗi xóa lớp học", error);
      }
    };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      const token = localStorage.getItem('token');
      if (editId) {
        await axios.put(`/class/${editId}`, formData, {
          headers: { Authorization: `Bearer ${token}` }
        });
      } else {
        await axios.post(`/class`, formData, {
          headers: { Authorization: `Bearer ${token}` }
        });
      }
      setShowAddModal(false);
      resetForm();
      fetchData();
    } catch (error) {
      console.error("Lỗi lưu lớp học", error);
    }
  };

  const handleViewHistory = async (id: number, name: string) => {
    try {
      const token = localStorage.getItem('token');
      const response = await axios.get(`/class/${id}/attendances`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      setAttendances(response.data);
      setSelectedClassName(name);
      setShowHistoryModal(true);
    } catch (error) {
      console.error("Lỗi lấy lịch sử điểm danh", error);
    }
  };

  return (
    <div className="space-y-6 animate-fade-in-up">
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 bg-white p-6 rounded-2xl shadow-sm border border-slate-100">
        <div>
          <h1 className="text-2xl font-bold text-slate-800">Quản lý Lớp học</h1>
          <p className="text-slate-500 text-sm mt-1">Danh sách các lớp học hiện có trong trung tâm</p>
        </div>
        <button 
          onClick={() => { resetForm(); setShowAddModal(true); }}
          className="w-full sm:w-auto flex items-center justify-center px-4 py-2 bg-indigo-600 text-white rounded-xl font-medium hover:bg-indigo-700 shadow-sm transition-colors"
        >
          <Plus size={18} className="mr-2" />
          Tạo lớp học
        </button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {classes.map((cls) => (
          <div key={cls.id} className="bg-white p-6 rounded-2xl shadow-sm border border-slate-100 flex flex-col justify-between group">
            <div>
              <div className="flex justify-between items-start mb-4">
                <span className="inline-flex items-center px-2.5 py-1 rounded-lg text-xs font-medium bg-purple-50 text-purple-700 border border-purple-100">
                  {cls.classCode}
                </span>
                <div className="flex space-x-1">
                  <button 
                    onClick={() => {
                      setEditId(cls.id);
                      setFormData({
                        classCode: cls.classCode,
                        className: cls.className,
                        timeSlots: cls.timeSlots || '',
                        studentIds: cls.studentIds || []
                      });
                      setShowAddModal(true);
                    }}
                    className="p-1.5 text-indigo-600 hover:bg-indigo-50 rounded-lg transition-colors"
                  >
                    <Edit2 size={16} />
                  </button>
                  <button onClick={() => handleDelete(cls.id)} className="p-1.5 text-red-600 hover:bg-red-50 rounded-lg transition-colors"><Trash2 size={16} /></button>
                </div>
              </div>
              <h3 className="text-xl font-bold text-slate-800 mb-2">{cls.className}</h3>
              <div className="text-sm text-slate-500 space-y-1">
                <p>Số buổi: {cls.totalSessions}</p>
                <p>Lịch học: {cls.timeSlots || 'Chưa thiết lập'}</p>
              </div>
            </div>
            
            <div className="mt-6 pt-4 border-t border-slate-100 flex space-x-3">
              <button onClick={() => handleViewHistory(cls.id, cls.className)} className="flex-1 flex justify-center items-center px-4 py-2 bg-slate-50 text-slate-600 rounded-xl font-medium hover:bg-slate-100 transition-colors text-sm">
                <History size={16} className="mr-2" />
                Lịch sử ĐD
              </button>
            </div>
          </div>
        ))}
      </div>

      {showHistoryModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm animate-fade-in-up">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-2xl max-h-[80vh] flex flex-col">
            <div className="p-6 border-b border-slate-100 flex justify-between items-center">
              <h3 className="text-xl font-bold text-slate-800">Lịch sử điểm danh: {selectedClassName}</h3>
              <button onClick={() => setShowHistoryModal(false)} className="text-slate-400 hover:text-slate-600 font-bold">Đóng</button>
            </div>
            <div className="p-6 overflow-y-auto">
              {attendances.length === 0 ? (
                <p className="text-center text-slate-500 py-8">Chưa có dữ liệu điểm danh nào.</p>
              ) : (
                <div className="space-y-4">
                  {attendances.map((att, idx) => (
                    <div key={idx} className="flex justify-between items-center p-4 bg-slate-50 rounded-xl border border-slate-100">
                      <div>
                        <p className="font-bold text-slate-800">{att.studentName}</p>
                        <p className="text-xs text-slate-500 mt-1">{new Date(att.date).toLocaleDateString('vi-VN')}</p>
                      </div>
                      <span className={`px-3 py-1 rounded-full text-xs font-bold ${att.isPresent ? 'bg-emerald-100 text-emerald-700' : 'bg-red-100 text-red-700'}`}>
                        {att.isPresent ? 'Có mặt' : 'Vắng mặt'}
                      </span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {showAddModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm animate-fade-in-up">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md flex flex-col">
            <div className="p-6 border-b border-slate-100 flex justify-between items-center">
              <h3 className="text-xl font-bold text-slate-800">{editId ? 'Sửa Lớp Học' : 'Thêm Lớp Mới'}</h3>
              <button onClick={() => setShowAddModal(false)} className="text-slate-400 hover:text-slate-600 font-bold">Đóng</button>
            </div>
            <div className="p-6">
              <form onSubmit={handleSubmit} className="space-y-4">
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Mã Lớp</label>
                  <input type="text" required placeholder="VD: TOAN6" value={formData.classCode} onChange={e => setFormData({...formData, classCode: e.target.value})} className="w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Tên Lớp</label>
                  <input type="text" required placeholder="VD: Toán Cơ bản Lớp 6" value={formData.className} onChange={e => setFormData({...formData, className: e.target.value})} className="w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Học viên tham gia</label>
                  <input 
                    type="text" 
                    placeholder="Tìm kiếm học viên..." 
                    value={searchStudent} 
                    onChange={e => setSearchStudent(e.target.value)} 
                    className="w-full border border-slate-200 rounded-xl px-4 py-2 mb-2 focus:ring-2 focus:ring-indigo-500/50" 
                  />
                  <div className="max-h-40 overflow-y-auto border border-slate-200 rounded-xl p-2 space-y-1">
                    {students.filter(s => s.fullName.toLowerCase().includes(searchStudent.toLowerCase())).map(student => {
                      const isCurrentSelected = formData.studentIds.includes(student.id);
                      const isInOtherClass = Boolean(student.classId && student.classId !== editId);
                      return (
                        <label 
                          key={student.id} 
                          className={`flex items-center space-x-2 p-2 rounded ${isInOtherClass ? 'opacity-60 bg-slate-50 cursor-not-allowed' : 'hover:bg-slate-50 cursor-pointer'}`}
                          title={isInOtherClass ? `Đang thuộc lớp: ${student.className || 'ID ' + student.classId}` : ''}
                        >
                          <input 
                            type="checkbox" 
                            checked={isCurrentSelected}
                            disabled={isInOtherClass}
                            onChange={(e) => {
                              const newIds = e.target.checked 
                                ? [...formData.studentIds, student.id]
                                : formData.studentIds.filter(id => id !== student.id);
                              setFormData({ ...formData, studentIds: newIds });
                            }}
                            className="rounded text-indigo-600 focus:ring-indigo-500 disabled:opacity-40"
                          />
                          <div className="flex-1 flex items-center justify-between">
                            <span className="text-sm font-medium text-slate-700">{student.fullName}</span>
                            <div className="flex items-center space-x-1.5">
                              {isInOtherClass && (
                                <span className="text-[10px] font-semibold px-1.5 py-0.5 rounded bg-amber-50 text-amber-700 border border-amber-200">
                                  {student.className || 'Đã có lớp'}
                                </span>
                              )}
                              <span className="text-xs text-slate-400">({student.parentPhoneNumber})</span>
                            </div>
                          </div>
                        </label>
                      );
                    })}
                    {students.length === 0 && <p className="text-xs text-slate-500 text-center py-2">Chưa có học viên nào trong hệ thống</p>}
                  </div>
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Khung giờ học (Tự do)</label>
                  <input type="text" placeholder="VD: T2, T4: 14h-15h30" value={formData.timeSlots} onChange={e => setFormData({...formData, timeSlots: e.target.value})} className="w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50" />
                </div>
                <div className="flex justify-end pt-4">
                  <button type="submit" className="px-6 py-2 bg-indigo-600 text-white font-bold rounded-xl shadow-md hover:bg-indigo-700 transition-colors">Lưu lại</button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default ClassList;
