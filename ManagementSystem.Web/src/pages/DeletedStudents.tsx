import React, { useState, useEffect } from 'react';
import { Users, Search, RefreshCw, Trash2, ArrowLeft, CheckSquare } from 'lucide-react';
import axios from 'axios';
import { Link } from 'react-router-dom';

interface Student {
  id: number;
  fullName: string;
  parentName: string;
  parentPhoneNumber: string;
  classId: number;
  totalRemainingSessions: number;
  totalAttendedSessions: number;
  totalPaidSessions: number;
  dateOfBirth?: string;
}

const DeletedStudents = () => {
  const [students, setStudents] = useState<Student[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedStudentIds, setSelectedStudentIds] = useState<number[]>([]);
  const [classes, setClasses] = useState<any[]>([]);

  useEffect(() => {
    fetchClasses();
    fetchDeletedStudents();
  }, []);

  const fetchClasses = async () => {
    try {
      const token = localStorage.getItem('token');
      const res = await axios.get(`/class`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      setClasses(res.data);
    } catch (error) {
      console.error("Lỗi lấy danh sách lớp", error);
    }
  };

  const fetchDeletedStudents = async () => {
    setLoading(true);
    try {
      const token = localStorage.getItem('token');
      const res = await axios.get(`/student/deleted`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      setStudents(res.data);
      setSelectedStudentIds([]);
    } catch (error) {
      console.error("Lỗi tải học sinh đã xóa", error);
    } finally {
      setLoading(false);
    }
  };

  const handleRecover = async (ids: number[]) => {
    if (!window.confirm(`Bạn có chắc muốn khôi phục ${ids.length} học sinh đã chọn?`)) return;
    try {
      const token = localStorage.getItem('token');
      await axios.put(`/student/recover`, ids, {
        headers: { Authorization: `Bearer ${token}` }
      });
      alert('Khôi phục thành công!');
      fetchDeletedStudents();
    } catch (error) {
      console.error("Lỗi khôi phục", error);
      alert("Khôi phục thất bại.");
    }
  };

  const toggleSelect = (id: number) => {
    if (selectedStudentIds.includes(id)) {
      setSelectedStudentIds(selectedStudentIds.filter(x => x !== id));
    } else {
      setSelectedStudentIds([...selectedStudentIds, id]);
    }
  };

  const toggleSelectAll = () => {
    if (selectedStudentIds.length === filteredStudents.length) {
      setSelectedStudentIds([]);
    } else {
      setSelectedStudentIds(filteredStudents.map(s => s.id));
    }
  };

  const filteredStudents = students.filter((s: Student) => 
    s.fullName.toLowerCase().includes(searchQuery.toLowerCase()) ||
    s.parentPhoneNumber.includes(searchQuery)
  );

  return (
    <div className="space-y-6 animate-fade-in-up">
      <div className="flex justify-between items-center bg-white p-6 rounded-2xl shadow-sm border border-slate-100">
        <div>
          <h1 className="text-2xl font-bold text-slate-800 flex items-center">
            <Trash2 className="mr-3 text-red-500" /> Thùng rác (Đã xóa)
          </h1>
          <p className="text-slate-500 text-sm mt-1">Khôi phục các học sinh đã bị xóa khỏi hệ thống</p>
        </div>
        <div className="flex space-x-3">
          {selectedStudentIds.length > 0 && (
            <button 
              onClick={() => handleRecover(selectedStudentIds)}
              className="flex items-center px-4 py-2 bg-emerald-600 text-white font-bold rounded-xl shadow-md hover:bg-emerald-700 transition-colors"
            >
              <RefreshCw size={18} className="mr-2" />
              Khôi phục ({selectedStudentIds.length})
            </button>
          )}
          <Link to="/admin/students" className="flex items-center px-4 py-2 bg-slate-100 text-slate-700 font-bold rounded-xl hover:bg-slate-200 transition-colors">
            <ArrowLeft size={18} className="mr-2" />
            Quay lại
          </Link>
        </div>
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-slate-100 overflow-hidden flex flex-col h-[calc(100vh-12rem)]">
        <div className="p-4 border-b border-slate-100 flex items-center gap-4 bg-slate-50/50 shrink-0">
          <div className="relative flex-1 max-w-md">
            <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
              <Search size={18} className="text-slate-400" />
            </div>
            <input
              type="text"
              placeholder="Tìm kiếm theo tên hoặc SĐT..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="pl-10 w-full border border-slate-200 rounded-xl px-4 py-2.5 focus:ring-2 focus:ring-indigo-500/50 bg-white shadow-sm transition-all text-sm"
            />
          </div>
        </div>

        <div className="flex-1 overflow-auto">
          <table className="w-full text-left border-collapse">
            <thead className="bg-slate-50 sticky top-0 z-10 shadow-sm">
              <tr>
                <th className="px-6 py-4 text-xs font-bold text-slate-500 uppercase tracking-wider border-b border-slate-200">
                  <div className="flex items-center space-x-2">
                    <input 
                      type="checkbox" 
                      className="w-4 h-4 text-indigo-600 rounded border-slate-300 focus:ring-indigo-500 cursor-pointer"
                      checked={selectedStudentIds.length === filteredStudents.length && filteredStudents.length > 0}
                      onChange={toggleSelectAll}
                    />
                  </div>
                </th>
                <th className="px-6 py-4 text-xs font-bold text-slate-500 uppercase tracking-wider border-b border-slate-200">ID</th>
                <th className="px-6 py-4 text-xs font-bold text-slate-500 uppercase tracking-wider border-b border-slate-200">Học sinh</th>
                <th className="px-6 py-4 text-xs font-bold text-slate-500 uppercase tracking-wider border-b border-slate-200">Phụ huynh</th>
                <th className="px-6 py-4 text-xs font-bold text-slate-500 uppercase tracking-wider border-b border-slate-200">Lớp</th>
                <th className="px-6 py-4 text-xs font-bold text-slate-500 uppercase tracking-wider border-b border-slate-200">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {loading ? (
                <tr>
                  <td colSpan={6} className="px-6 py-12 text-center text-slate-500">
                    <div className="flex flex-col items-center justify-center">
                      <RefreshCw className="w-8 h-8 text-indigo-400 animate-spin mb-4" />
                      <p>Đang tải dữ liệu...</p>
                    </div>
                  </td>
                </tr>
              ) : filteredStudents.map((student: Student) => (
                <tr key={student.id} className="hover:bg-slate-50 transition-colors group cursor-pointer" onClick={() => toggleSelect(student.id)}>
                  <td className="px-6 py-4" onClick={e => e.stopPropagation()}>
                    <input 
                      type="checkbox" 
                      className="w-4 h-4 text-indigo-600 rounded border-slate-300 focus:ring-indigo-500 cursor-pointer"
                      checked={selectedStudentIds.includes(student.id)}
                      onChange={() => toggleSelect(student.id)}
                    />
                  </td>
                  <td className="px-6 py-4 text-sm text-slate-500">#{student.id}</td>
                  <td className="px-6 py-4">
                    <div className="font-bold text-slate-800">{student.fullName}</div>
                    {student.dateOfBirth && (
                      <div className="text-xs text-slate-500 mt-1">SN: {new Date(student.dateOfBirth).getFullYear()}</div>
                    )}
                  </td>
                  <td className="px-6 py-4">
                    <div className="text-sm font-medium text-slate-800">{student.parentName}</div>
                    <div className="text-xs text-slate-500 flex items-center mt-1">
                      {student.parentPhoneNumber}
                    </div>
                  </td>
                  <td className="px-6 py-4">
                    <span className="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium bg-indigo-50 text-indigo-700 border border-indigo-100">
                      {classes.find((c: any) => c.id === student.classId)?.className || 'Chưa xếp lớp'}
                    </span>
                  </td>
                  <td className="px-6 py-4" onClick={e => e.stopPropagation()}>
                    <button 
                      onClick={() => handleRecover([student.id])}
                      className="flex items-center px-3 py-1.5 bg-emerald-50 text-emerald-600 rounded-lg hover:bg-emerald-100 transition-colors text-xs font-bold"
                    >
                      <RefreshCw size={14} className="mr-1" />
                      Khôi phục
                    </button>
                  </td>
                </tr>
              ))}
              {filteredStudents.length === 0 && !loading && (
                <tr>
                  <td colSpan={6} className="p-12 text-center text-slate-500">
                    <Trash2 className="w-12 h-12 text-slate-200 mx-auto mb-4" />
                    Thùng rác trống. Không tìm thấy học sinh nào.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};

export default DeletedStudents;
