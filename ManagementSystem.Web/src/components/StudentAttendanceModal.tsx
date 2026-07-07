import React, { useState, useEffect } from 'react';
import axios from 'axios';

interface StudentAttendanceModalProps {
  isOpen: boolean;
  onClose: () => void;
  student: any;
}

const StudentAttendanceModal: React.FC<StudentAttendanceModalProps> = ({ isOpen, onClose, student }) => {
  const [attendances, setAttendances] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [filterMonth, setFilterMonth] = useState<string>(''); // format YYYY-MM

  useEffect(() => {
    if (isOpen && student) {
      fetchAttendances();
      // Default filter to current month
      const now = new Date();
      setFilterMonth(`${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`);
    }
  }, [isOpen, student]);

  const fetchAttendances = async () => {
    setLoading(true);
    try {
      const token = localStorage.getItem('token');
      const res = await axios.get(`/attendance/student/${student.id}`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      setAttendances(res.data);
    } catch (err) {
      console.error("Lỗi lấy danh sách điểm danh", err);
    } finally {
      setLoading(false);
    }
  };

  if (!isOpen || !student) return null;

  // Filter dates
  const filteredDates = attendances.filter(dateStr => {
    if (!filterMonth) return true; // Show all
    const d = new Date(dateStr);
    const monthStr = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
    return monthStr === filterMonth;
  });

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm">
      <div className="bg-white rounded-2xl w-full max-w-md shadow-xl overflow-hidden flex flex-col max-h-[80vh]">
        
        {/* Header */}
        <div className="px-6 py-4 border-b border-slate-100 flex justify-between items-center bg-slate-50">
          <div>
            <h3 className="text-lg font-bold text-slate-800">Lịch sử điểm danh</h3>
            <p className="text-sm text-slate-500">{student.fullName}</p>
          </div>
          <button 
            onClick={onClose}
            className="p-2 text-slate-400 hover:text-slate-600 hover:bg-slate-200 rounded-lg transition-colors"
          >
            <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Body */}
        <div className="p-6 overflow-y-auto flex-1">
          <div className="mb-4">
            <label className="block text-sm font-medium text-slate-700 mb-1">Lọc theo Tháng/Năm</label>
            <input 
              type="month" 
              className="w-full px-4 py-2 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
              value={filterMonth}
              onChange={(e) => setFilterMonth(e.target.value)}
            />
          </div>

          {loading ? (
            <div className="flex justify-center py-8">
              <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-500"></div>
            </div>
          ) : (
            <div>
              <div className="text-sm font-medium text-slate-500 mb-2">
                Tổng số buổi đã học trong tháng: {filteredDates.length}
              </div>
              {filteredDates.length === 0 ? (
                <div className="text-center py-8 text-slate-500 bg-slate-50 rounded-xl">
                  Không có dữ liệu điểm danh trong tháng này
                </div>
              ) : (
                <ul className="space-y-2">
                  {filteredDates.map((dateStr, index) => {
                    const d = new Date(dateStr);
                    const formattedDate = `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}/${d.getFullYear()}`;
                    return (
                      <li key={index} className="flex items-center justify-between p-3 bg-slate-50 rounded-xl border border-slate-100">
                        <span className="font-medium text-slate-700">{formattedDate}</span>
                        <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-green-100 text-green-800">
                          Có mặt
                        </span>
                      </li>
                    );
                  })}
                </ul>
              )}
            </div>
          )}
        </div>
        
        {/* Footer */}
        <div className="px-6 py-4 bg-slate-50 border-t border-slate-100 flex justify-end">
          <button 
            onClick={onClose}
            className="px-4 py-2 text-slate-600 bg-white border border-slate-200 rounded-xl hover:bg-slate-50 font-medium transition-colors"
          >
            Đóng
          </button>
        </div>
      </div>
    </div>
  );
};

export default StudentAttendanceModal;
