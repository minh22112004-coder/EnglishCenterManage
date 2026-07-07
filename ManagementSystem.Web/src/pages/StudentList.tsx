import React, { useState, useEffect } from 'react';
import { Search, Plus, Filter, MoreVertical, Edit2, Trash2, Download, Upload, RefreshCw } from 'lucide-react';
import axios from 'axios';
import * as xlsx from 'xlsx';
import StudentAttendanceModal from '../components/StudentAttendanceModal';

interface Student {
  id: number;
  fullName: string;
  parentName: string;
  parentPhoneNumber: string;
  classId: number;
  totalRemainingSessions: number;
  totalAttendedSessions: number;
  totalPaidSessions: number;
  attendedDates?: string[];
  dateOfBirth?: string;
}

type SortKey = 'id' | 'fullName' | 'totalRemainingSessions';
type SortDirection = 'asc' | 'desc';

const StudentList = () => {
  const [students, setStudents] = useState<Student[]>([]);
  const [loading, setLoading] = useState(true);
  
  const [searchQuery, setSearchQuery] = useState('');
  const [showAddModal, setShowAddModal] = useState(false);
  const [showEditModal, setShowEditModal] = useState(false);
  const [classes, setClasses] = useState<any[]>([]);
  const [formData, setFormData] = useState({ fullName: '', parentName: '', parentPhoneNumber: '', classId: '', totalRemainingSessions: 10, totalAttendedSessions: 0, totalPaidSessions: 0 });
  const [editStudentData, setEditStudentData] = useState<Student | null>(null);
  
  const [sortConfig, setSortConfig] = useState<{ key: SortKey; direction: SortDirection } | null>(null);
  const [attendanceModalOpen, setAttendanceModalOpen] = useState(false);
  const [selectedStudentForAttendance, setSelectedStudentForAttendance] = useState<Student | null>(null);
  const [selectedStudentIds, setSelectedStudentIds] = useState<number[]>([]);
  const [showQuickSelect, setShowQuickSelect] = useState(false);
  
  const [isAdmin, setIsAdmin] = useState(false);
  
  useEffect(() => {
    const token = localStorage.getItem('token');
    if (token) {
        try {
            const payload = JSON.parse(atob(token.split('.')[1]));
            const roles = payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
            setIsAdmin(roles === 'Admin' || (Array.isArray(roles) && roles.includes('Admin')));
        } catch (e) {
            console.error('Invalid token', e);
        }
    }
  }, []);
  
  const fileInputRef = React.useRef<HTMLInputElement>(null);

  const resetForm = () => {
    setFormData({ fullName: '', parentName: '', parentPhoneNumber: '', classId: '', totalRemainingSessions: 10, totalAttendedSessions: 0, totalPaidSessions: 0 });
  };

  const fetchStudents = async () => {
    try {
      setLoading(true);
      const token = localStorage.getItem('token');
      const searchParam = searchQuery ? `?search=${encodeURIComponent(searchQuery)}` : '';
      const response = await axios.get(`/student${searchParam}`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      setStudents(response.data);
    } catch (error) {
      console.error("Lỗi lấy danh sách học sinh", error);
    } finally {
      setLoading(false);
    }
  };

  const fetchClasses = async () => {
    try {
      const token = localStorage.getItem('token');
      const response = await axios.get(`/class`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      setClasses(response.data);
    } catch (error) {
      console.error("Lỗi lấy danh sách lớp học", error);
    }
  };

  useEffect(() => {
    fetchStudents();
    fetchClasses();
  }, []);

  const handleSort = (key: SortKey) => {
    let direction: SortDirection = 'asc';
    if (sortConfig && sortConfig.key === key && sortConfig.direction === 'asc') {
      direction = 'desc';
    }
    setSortConfig({ key, direction });
  };

  const filteredStudents = students.filter(s => 
    s.fullName.toLowerCase().includes(searchQuery.toLowerCase()) || 
    s.parentPhoneNumber.includes(searchQuery)
  );

  const sortedStudents = React.useMemo(() => {
    let sortableItems = [...filteredStudents];
    if (sortConfig !== null) {
      sortableItems.sort((a, b) => {
        if (a[sortConfig.key] < b[sortConfig.key]) {
          return sortConfig.direction === 'asc' ? -1 : 1;
        }
        if (a[sortConfig.key] > b[sortConfig.key]) {
          return sortConfig.direction === 'asc' ? 1 : -1;
        }
        return 0;
      });
    }
    return sortableItems;
  }, [filteredStudents, sortConfig]);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    fetchStudents();
  };

  const handleDelete = async (id: number) => {
    if (!window.confirm("Bạn có chắc chắn muốn xóa học sinh này không?")) return;
    try {
      const token = localStorage.getItem('token');
      await axios.delete(`/student/${id}`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      fetchStudents();
      setSelectedStudentIds(prev => prev.filter(sId => sId !== id));
    } catch (error) {
      console.error("Lỗi xóa học sinh", error);
    }
  };

  const handleBulkDelete = async () => {
    if (selectedStudentIds.length === 0) return;
    if (!window.confirm(`Bạn có chắc chắn muốn xóa ${selectedStudentIds.length} học sinh đã chọn không?`)) return;
    try {
      const token = localStorage.getItem('token');
      await axios.delete(`/student/bulk`, {
        headers: { Authorization: `Bearer ${token}` },
        data: selectedStudentIds
      });
      setSelectedStudentIds([]);
      fetchStudents();
    } catch (error) {
      console.error("Lỗi xóa hàng loạt", error);
      alert("Xóa thất bại.");
    }
  };

  const handleSyncAccounts = async () => {
    if (!window.confirm("Bạn có muốn quét và đồng bộ tạo tài khoản cho tất cả phụ huynh chưa có tài khoản không?")) return;
    try {
      const token = localStorage.getItem('token');
      const res = await axios.post(`/student/sync-accounts`, {}, {
        headers: { Authorization: `Bearer ${token}` }
      });
      alert(res.data.message || "Đồng bộ thành công!");
    } catch (error) {
      console.error("Lỗi đồng bộ", error);
      alert("Đồng bộ thất bại.");
    }
  };

  const toggleSelectStudent = (id: number) => {
    setSelectedStudentIds(prev => 
      prev.includes(id) ? prev.filter(sid => sid !== id) : [...prev, id]
    );
  };

  const handleQuickSelect = (type: string, value: string = '') => {
    if (type === 'all') {
      setSelectedStudentIds(sortedStudents.map(s => s.id));
    } else if (type === 'none') {
      setSelectedStudentIds([]);
    } else if (type === 'class') {
      const classId = parseInt(value);
      setSelectedStudentIds(sortedStudents.filter(s => s.classId === classId).map(s => s.id));
    } else if (type === 'year') {
      setSelectedStudentIds(sortedStudents.filter(s => s.dateOfBirth && s.dateOfBirth.startsWith(value)).map(s => s.id));
    }
    setShowQuickSelect(false);
  };

  const handleUpdate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editStudentData) return;
    try {
      const token = localStorage.getItem('token');
      await axios.put(`/student/${editStudentData.id}`, editStudentData, {
        headers: { Authorization: `Bearer ${token}` }
      });
      setShowEditModal(false);
      fetchStudents();
    } catch (error) {
      console.error("Lỗi cập nhật học sinh", error);
      alert("Lỗi cập nhật học sinh");
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      const token = localStorage.getItem('token');
      const payload = {
        ...formData,
        classId: parseInt(formData.classId),
        totalRemainingSessions: formData.totalRemainingSessions,
        dateOfBirth: new Date().toISOString()
      };

      await axios.post(`/student`, payload, {
        headers: { Authorization: `Bearer ${token}` }
      });

      setShowAddModal(false);
      resetForm();
      fetchStudents();
    } catch (error) {
      console.error("Lỗi lưu học sinh", error);
      alert("Lỗi lưu học sinh");
    }
  };

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = async (evt) => {
      try {
        setLoading(true);
        const bstr = evt.target?.result;
        const wb = xlsx.read(bstr, { type: 'binary' });
        const wsname = wb.SheetNames[0];
        const ws = wb.Sheets[wsname];
        
        const parseExcelDate = (excelDate: any): string => {
          if (!excelDate) return new Date(Date.UTC(2000, 0, 1)).toISOString();
          
          let numVal = Number(excelDate);
          if (!isNaN(numVal) && numVal > 20000 && numVal < 60000) {
            return new Date(Math.round((numVal - 25569) * 86400 * 1000)).toISOString();
          }
          if (!isNaN(numVal) && numVal > 1900 && numVal <= new Date().getFullYear()) {
            return new Date(Date.UTC(numVal, 0, 1)).toISOString();
          }

          if (typeof excelDate === 'string') {
            const parts = excelDate.split(/[\/\-]/);
            if (parts.length === 3) {
              let day = parseInt(parts[0]);
              let month = parseInt(parts[1]) - 1;
              let year = parseInt(parts[2]);
              
              if (year < 100) year += 2000;
              
              if (year > 1900 && year < 2100) {
                return new Date(Date.UTC(year, month, day)).toISOString();
              }
            }
          }
          return new Date(Date.UTC(2000, 0, 1)).toISOString();
        };

          const parseBirthYear = (excelDate: any): string => {
            if (!excelDate) return new Date(Date.UTC(2000, 0, 1)).toISOString();
            
            let numVal = Number(excelDate);
            if (!isNaN(numVal) && numVal > 20000 && numVal < 60000) {
              const date = new Date(Math.round((numVal - 25569) * 86400 * 1000));
              return new Date(Date.UTC(date.getFullYear(), 0, 1)).toISOString();
            }
            if (!isNaN(numVal) && numVal > 1900 && numVal <= new Date().getFullYear()) {
              return new Date(Date.UTC(numVal, 0, 1)).toISOString();
            }

            if (typeof excelDate === 'string') {
              const parts = excelDate.split(/[\/\-\.]/);
              for (let part of parts) {
                let p = parseInt(part);
                if (p > 1900 && p < 2100) return new Date(Date.UTC(p, 0, 1)).toISOString();
              }
            }
            return new Date(Date.UTC(2000, 0, 1)).toISOString();
          };

          const rawRows = xlsx.utils.sheet_to_json<any[]>(ws, { header: 1 });
        const headerRowIndex = rawRows.findIndex(row => row.includes('Họ và Tên') || row.includes('Phụ huynh'));
        
        if (headerRowIndex === -1) {
          throw new Error("Không tìm thấy dòng tiêu đề (cần có cột 'Họ và Tên' hoặc 'Phụ huynh').");
        }

        const data = xlsx.utils.sheet_to_json<any>(ws, { range: headerRowIndex, defval: "", raw: false });
        
        const dtos = data.map((row: any) => {
          const attendedDates: string[] = [];
          Object.keys(row).forEach(key => {
            const lowerKey = key.toLowerCase();
            if (lowerKey.includes('stt') || lowerKey.includes('họ và tên') || lowerKey.includes('phụ huynh') || lowerKey.includes('số điện thoại') || lowerKey.includes('dạng lớp') || lowerKey.includes('năm sinh') || lowerKey.includes('buổi đã học') || lowerKey.includes('buổi đã đóng') || lowerKey.includes('buổi còn lại')) return;
            
            const val = row[key];
            if (val) {
                const parsed = parseExcelDate(val);
                if (parsed && !parsed.startsWith("2000-01-01")) {
                    attendedDates.push(parsed);
                }
            }
          });
          let rawPhone = row["Số điện thoại"]?.toString() || "";
          let phoneStr = "";
          if (rawPhone) {
            let cleanedPhone = rawPhone.replace(/[\s\.\-\(\)]+/g, '');
            let match = cleanedPhone.match(/(0|84)\d{9}/);
            if (match) {
              phoneStr = match[0];
              if (phoneStr.startsWith("84") && phoneStr.length === 11) {
                phoneStr = "0" + phoneStr.substring(2);
              }
            }
          }
          
          let parentName = row["Phụ huynh"]?.toString() || "";
          if (rawPhone && /[a-zA-Z]/i.test(rawPhone)) {
             parentName = parentName ? `${parentName} (${rawPhone})` : rawPhone;
          }

          return {
            fullName: row["Họ và Tên"] || "",
            parentName: parentName,
            parentPhoneNumber: phoneStr,
            className: row["Dạng lớp"] || "",
            dateOfBirth: parseBirthYear(row["Năm sinh"]),
            totalAttendedSessions: parseInt(row["Số buổi đã học"]) || 0,
            totalPaidSessions: parseInt(row["Số buổi đã đóng"]) || 0,
            totalRemainingSessions: parseInt(row["Số buổi còn lại"]) || 0,
            attendedDates: attendedDates
          };
        }).filter((d: any) => d.fullName);

        const token = localStorage.getItem('token');
        await axios.post(`/student/import`, dtos, {
          headers: { Authorization: `Bearer ${token}` }
        });
        alert("Import thành công!");
        fetchStudents();
      } catch (err: any) {
        console.error("Lỗi import file:", err);
        const serverMsg = err.response?.data?.message || err.response?.data?.inner;
        alert("Lỗi import: " + (serverMsg ? serverMsg : (err instanceof Error ? err.message : "Vui lòng kiểm tra định dạng.")));
      } finally {
        setLoading(false);
      }
    };
    reader.readAsBinaryString(file);
  };

  return (
    <div className="space-y-6 animate-fade-in-up">
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 bg-white p-6 rounded-2xl shadow-sm border border-slate-100">
        <div>
          <h1 className="text-2xl font-bold text-slate-800">Quản lý Học sinh</h1>
          <p className="text-slate-500 text-sm mt-1">Danh sách học viên và thông tin phụ huynh</p>
        </div>
        <div className="flex w-full sm:w-auto items-center space-x-3">
          <input 
            type="file" 
            accept=".xlsx, .xls" 
            ref={fileInputRef} 
            onChange={handleFileUpload} 
            className="hidden" 
          />
          <button 
            onClick={() => fileInputRef.current?.click()}
            className="flex-1 sm:flex-none flex items-center justify-center px-4 py-2 bg-emerald-50 text-emerald-600 rounded-xl font-medium hover:bg-emerald-100 border border-emerald-200 transition-colors"
          >
            <Upload size={18} className="mr-2" />
            Nhập Excel
          </button>
          
          {isAdmin && selectedStudentIds.length > 0 && (
            <button 
              onClick={handleBulkDelete}
              className="flex-1 sm:flex-none flex items-center justify-center px-4 py-2 bg-red-50 text-red-600 rounded-xl font-medium hover:bg-red-100 border border-red-200 transition-colors"
            >
              <Trash2 size={18} className="mr-2" />
              Xóa ({selectedStudentIds.length})
            </button>
          )}

          <div className="relative">
            <button 
              onClick={() => setShowQuickSelect(!showQuickSelect)}
              className="flex-1 sm:flex-none flex items-center justify-center px-4 py-2 bg-slate-100 text-slate-700 rounded-xl font-medium hover:bg-slate-200 transition-colors"
            >
              Chọn nhanh
            </button>
            {showQuickSelect && (
              <div className="absolute right-0 mt-2 w-48 bg-white border border-slate-200 shadow-lg rounded-xl z-10 py-2">
                <button onClick={() => handleQuickSelect('all')} className="w-full text-left px-4 py-2 text-sm text-slate-700 hover:bg-indigo-50 hover:text-indigo-600">Chọn tất cả</button>
                <button onClick={() => handleQuickSelect('none')} className="w-full text-left px-4 py-2 text-sm text-slate-700 hover:bg-indigo-50 hover:text-indigo-600 border-b border-slate-100">Bỏ chọn tất cả</button>
                <div className="px-4 py-2 text-xs font-semibold text-slate-400 uppercase">Theo lớp</div>
                {classes.map(c => (
                  <button key={c.id} onClick={() => handleQuickSelect('class', c.id.toString())} className="w-full text-left px-4 py-1.5 text-sm text-slate-700 hover:bg-indigo-50 hover:text-indigo-600">
                    {c.className}
                  </button>
                ))}
                <div className="px-4 py-2 text-xs font-semibold text-slate-400 uppercase border-t border-slate-100 mt-1">Theo năm sinh</div>
                {Array.from(new Set(students.filter(s => s.dateOfBirth).map(s => s.dateOfBirth!.substring(0, 4)))).sort().map(year => (
                  <button key={year} onClick={() => handleQuickSelect('year', year)} className="w-full text-left px-4 py-1.5 text-sm text-slate-700 hover:bg-indigo-50 hover:text-indigo-600">
                    Năm {year}
                  </button>
                ))}
              </div>
            )}
          </div>

            {isAdmin && (
              <button 
                onClick={handleSyncAccounts}
                className="flex-1 sm:flex-none flex items-center justify-center px-4 py-2 bg-blue-50 text-blue-600 rounded-xl font-medium hover:bg-blue-100 border border-blue-200 transition-colors"
              >
                <RefreshCw size={18} className="mr-2" />
                Đồng bộ
              </button>
            )}

            <button 
              onClick={() => { resetForm(); setShowAddModal(true); }}
            className="flex-1 sm:flex-none flex items-center justify-center px-4 py-2 bg-indigo-600 text-white rounded-xl font-medium hover:bg-indigo-700 shadow-sm transition-colors"
          >
            <Plus size={18} className="mr-2" />
            Thêm Học sinh
          </button>
        </div>
      </div>

      <div className="bg-white p-4 rounded-2xl shadow-sm border border-slate-100 flex flex-col md:flex-row md:items-center justify-between gap-4">
        <form onSubmit={handleSearch} className="relative flex-1 max-w-md">
          <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-slate-400" size={20} />
          <input 
            type="text" 
            placeholder="Tìm kiếm theo Tên hoặc Số điện thoại phụ huynh..." 
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="w-full pl-10 pr-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-indigo-500/50 focus:bg-white transition-all text-sm"
          />
          <button type="submit" className="hidden">Tìm</button>
        </form>
        <div className="flex items-center space-x-2">
          <button className="p-2.5 text-slate-500 hover:bg-slate-50 rounded-xl transition-colors border border-slate-200 flex items-center">
            <Filter size={18} className="mr-2" /> Lọc
          </button>
        </div>
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-slate-100 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse min-w-[800px]">
            <thead className="bg-slate-50 text-slate-500 text-xs font-semibold uppercase tracking-wider">
              <tr>
                <th className="px-6 py-4 text-left w-12">
                  <input 
                    type="checkbox" 
                    className="w-4 h-4 text-indigo-600 rounded border-slate-300 focus:ring-indigo-500"
                    checked={sortedStudents.length > 0 && selectedStudentIds.length === sortedStudents.length}
                    onChange={(e) => {
                      if (e.target.checked) handleQuickSelect('all');
                      else handleQuickSelect('none');
                    }}
                  />
                </th>
                <th className="px-6 py-4 text-left cursor-pointer hover:bg-slate-100" onClick={() => handleSort('id')}>
                  ID {sortConfig?.key === 'id' ? (sortConfig.direction === 'asc' ? '↑' : '↓') : ''}
                </th>
                <th className="px-6 py-4 text-left cursor-pointer hover:bg-slate-100" onClick={() => handleSort('fullName')}>
                  TÊN HỌC SINH {sortConfig?.key === 'fullName' ? (sortConfig.direction === 'asc' ? '↑' : '↓') : ''}
                </th>
                <th className="px-6 py-4 text-left">PHỤ HUYNH</th>
                <th className="px-6 py-4 text-left">SỐ ĐIỆN THOẠI</th>
                <th className="px-6 py-4 text-center cursor-pointer hover:bg-slate-100" onClick={() => handleSort('totalRemainingSessions')}>
                  SỐ BUỔI CÒN LẠI {sortConfig?.key === 'totalRemainingSessions' ? (sortConfig.direction === 'asc' ? '↑' : '↓') : ''}
                </th>
                <th className="px-6 py-4 text-left">TRẠNG THÁI</th>
                <th className="px-6 py-4 text-right">HÀNH ĐỘNG</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {sortedStudents.map((student) => (
                <tr key={student.id} className="hover:bg-slate-50/80 transition-colors group">
                  <td className="px-6 py-4">
                    <input 
                      type="checkbox" 
                      className="w-4 h-4 text-indigo-600 rounded border-slate-300 focus:ring-indigo-500"
                      checked={selectedStudentIds.includes(student.id)}
                      onChange={() => toggleSelectStudent(student.id)}
                    />
                  </td>
                  <td className="p-4 text-slate-500 font-medium">#{student.id}</td>
                  <td className="p-4 font-bold text-slate-800">{student.fullName}</td>
                  <td className="p-4 text-slate-600">{student.parentName}</td>
                  <td className="p-4 text-slate-600">{student.parentPhoneNumber}</td>
                  <td className="px-6 py-4 whitespace-nowrap text-center">
                    <span 
                      className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-100 text-blue-800 cursor-pointer hover:bg-blue-200"
                      onClick={() => { setSelectedStudentForAttendance(student); setAttendanceModalOpen(true); }}
                      title="Xem lịch sử điểm danh"
                    >
                      {student.totalRemainingSessions} buổi
                    </span>
                  </td>
                  <td className="p-4">
                    <span className="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium bg-emerald-50 text-emerald-700">
                      <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 mr-1.5"></span>
                      Đang học
                    </span>
                  </td>
                  <td className="p-4 text-right">
                    <div className="flex items-center justify-end space-x-2">
                      <button 
                        onClick={() => {
                          setEditStudentData(student);
                          setShowEditModal(true);
                        }}
                        className="p-1.5 text-slate-400 hover:text-indigo-600 hover:bg-indigo-50 rounded-lg transition-colors"
                      >
                        <Edit2 size={16} />
                      </button>
                      {isAdmin && (
                        <button onClick={() => handleDelete(student.id)} className="p-1.5 text-slate-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors">
                          <Trash2 size={16} />
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
              {students.length === 0 && !loading && (
                <tr>
                  <td colSpan={8} className="p-8 text-center text-slate-500">Không tìm thấy học sinh nào.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Modal Thêm học sinh */}
      {showAddModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm animate-fade-in-up">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md flex flex-col">
            <div className="p-6 border-b border-slate-100 flex justify-between items-center">
              <h3 className="text-xl font-bold text-slate-800">Thêm Học Sinh Mới</h3>
              <button onClick={() => setShowAddModal(false)} className="text-slate-400 hover:text-slate-600 font-bold">Đóng</button>
            </div>
            <div className="p-6">
              <form onSubmit={handleSubmit} className="space-y-4">
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Tên học sinh</label>
                  <input type="text" required value={formData.fullName} onChange={e => setFormData({...formData, fullName: e.target.value})} className="w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Tên Phụ huynh</label>
                  <input type="text" required value={formData.parentName} onChange={e => setFormData({...formData, parentName: e.target.value})} className="w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Điện thoại Phụ huynh (Không bắt buộc)</label>
                  <input type="tel" value={formData.parentPhoneNumber} onChange={e => setFormData({...formData, parentPhoneNumber: e.target.value})} className="w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Chọn Lớp</label>
                  <select required value={formData.classId} onChange={e => setFormData({...formData, classId: e.target.value})} className="w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50">
                    <option value="">-- Chọn Lớp --</option>
                    {classes.map(c => (
                      <option key={c.id} value={c.id}>{c.className}</option>
                    ))}
                  </select>
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-medium text-slate-700 mb-1">Số buổi còn lại</label>
                    <input type="number" required value={formData.totalRemainingSessions} onChange={e => setFormData({...formData, totalRemainingSessions: parseInt(e.target.value) || 0})} className="w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50" />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-slate-700 mb-1">Số buổi đã đóng</label>
                    <input type="number" value={formData.totalPaidSessions} onChange={e => setFormData({...formData, totalPaidSessions: parseInt(e.target.value) || 0})} className="w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50" />
                  </div>
                </div>
                <div className="flex justify-end pt-4">
                  <button type="submit" className="px-6 py-2 bg-indigo-600 text-white font-bold rounded-xl shadow-md hover:bg-indigo-700 transition-colors">Lưu lại</button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}

      {/* Modal Chỉnh sửa học sinh */}
      {showEditModal && editStudentData && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm animate-fade-in-up">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md flex flex-col max-h-[90vh] overflow-y-auto">
            <div className="p-6 border-b border-slate-100 flex justify-between items-center sticky top-0 bg-white z-10">
              <h3 className="text-xl font-bold text-slate-800">Sửa Thông tin Học Sinh</h3>
              <button onClick={() => setShowEditModal(false)} className="text-slate-400 hover:text-slate-600 font-bold">Đóng</button>
            </div>
            <div className="p-6">
              <form onSubmit={handleUpdate} className="space-y-4">
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Tên học sinh</label>
                  <input type="text" required value={editStudentData.fullName} onChange={e => setEditStudentData({...editStudentData, fullName: e.target.value})} className="w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Tên Phụ huynh</label>
                  <input type="text" required value={editStudentData.parentName} onChange={e => setEditStudentData({...editStudentData, parentName: e.target.value})} className="w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Điện thoại Phụ huynh</label>
                  <input type="tel" value={editStudentData.parentPhoneNumber} onChange={e => setEditStudentData({...editStudentData, parentPhoneNumber: e.target.value})} className="w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Năm sinh / Ngày sinh (YYYY-MM-DD)</label>
                  <input type="text" value={editStudentData.dateOfBirth ? editStudentData.dateOfBirth.substring(0, 10) : ''} onChange={e => setEditStudentData({...editStudentData, dateOfBirth: new Date(e.target.value).toISOString()})} className="w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Chọn Lớp</label>
                  <select required value={editStudentData.classId} onChange={e => setEditStudentData({...editStudentData, classId: parseInt(e.target.value)})} className="w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50">
                    <option value="">-- Chọn Lớp --</option>
                    {classes.map(c => (
                      <option key={c.id} value={c.id}>{c.className}</option>
                    ))}
                  </select>
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-medium text-slate-700 mb-1">Số buổi đã học</label>
                    <input type="number" required value={editStudentData.totalAttendedSessions} onChange={e => setEditStudentData({...editStudentData, totalAttendedSessions: parseInt(e.target.value) || 0})} className="w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50" />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-slate-700 mb-1">Số buổi đã đóng</label>
                    <input type="number" required value={editStudentData.totalPaidSessions} onChange={e => setEditStudentData({...editStudentData, totalPaidSessions: parseInt(e.target.value) || 0})} className="w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50" />
                  </div>
                </div>
                <div className="flex justify-end pt-4">
                  <button type="submit" className="px-6 py-2 bg-indigo-600 text-white font-bold rounded-xl shadow-md hover:bg-indigo-700 transition-colors">Cập nhật</button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}

      {selectedStudentForAttendance && (
        <StudentAttendanceModal 
          isOpen={attendanceModalOpen} 
          onClose={() => { setAttendanceModalOpen(false); setSelectedStudentForAttendance(null); }} 
          student={selectedStudentForAttendance} 
        />
      )}
    </div>
  );
};

export default StudentList;
