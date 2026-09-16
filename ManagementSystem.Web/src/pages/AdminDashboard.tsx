import React, { useState, useEffect, useRef } from 'react';
import axios from 'axios';
import { Users, BookOpen, Calendar as CalendarIcon, Download } from 'lucide-react';

const AdminDashboard = () => {
  const [stats, setStats] = useState({
    totalStudents: 0,
    activeClasses: 0,
    todaySchedules: 0
  });
  const [warningStudents, setWarningStudents] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const hasFetchedRef = useRef(false);

  useEffect(() => {
    if (hasFetchedRef.current) return;
    hasFetchedRef.current = true;

    const fetchStats = async () => {
      try {
        const token = localStorage.getItem('token');
        const headers = { Authorization: `Bearer ${token}` };

        const [studentsRes, classesRes, schedulesRes] = await Promise.all([
          // This is the dashboard's primary authenticated bootstrap request.
          // Auxiliary requests must not clear the session independently.
          axios.get(`/student`, { headers, authFailureAction: 'logout' }),
          axios.get(`/class`, { headers }),
          axios.get(`/workschedule`, { headers })
        ]);

        const todayStr = new Date().toISOString().split('T')[0];
        const todaySchedulesCount = schedulesRes.data.filter((s: any) => {
          const sDate = new Date(s.date).toISOString().split('T')[0];
          return sDate === todayStr;
        }).length;

        setStats({
          totalStudents: studentsRes.data.length,
          activeClasses: classesRes.data.length,
          todaySchedules: todaySchedulesCount
        });

        // Lọc học sinh có số buổi còn lại < 3
        const warnings = studentsRes.data.filter((s: any) => s.totalRemainingSessions < 3);
        setWarningStudents(warnings);
      } catch (error) {
        console.error("Lỗi lấy dữ liệu thống kê:", error);
      } finally {
        setLoading(false);
      }
    };

    fetchStats();
  }, []);

  return (
    <div className="space-y-6 animate-fade-in-up">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-8">
        <h1 className="text-3xl font-bold text-slate-800 tracking-tight">Tổng quan hệ thống</h1>
        <button className="flex items-center justify-center bg-indigo-600 hover:bg-indigo-700 text-white px-4 py-2 rounded-xl font-medium shadow-sm transition-colors w-full sm:w-auto">
          <Download size={18} className="mr-2" />
          Tải báo cáo
        </button>
      </div>
      
      {loading ? (
        <div className="text-center p-8 text-slate-500">Đang tải dữ liệu...</div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          <div className="bg-white p-6 rounded-2xl shadow-sm border border-slate-100 transform hover:-translate-y-1 transition-transform">
            <div className="flex items-center mb-4">
              <div className="p-3 bg-indigo-50 text-indigo-600 rounded-xl">
                <Users size={24} />
              </div>
            </div>
            <h3 className="text-lg font-semibold text-slate-500">Tổng học sinh</h3>
            <div className="mt-2 flex items-baseline">
              <p className="text-5xl font-bold text-indigo-600">{stats.totalStudents}</p>
            </div>
          </div>
          
          <div className="bg-white p-6 rounded-2xl shadow-sm border border-slate-100 transform hover:-translate-y-1 transition-transform">
            <div className="flex items-center mb-4">
              <div className="p-3 bg-purple-50 text-purple-600 rounded-xl">
                <BookOpen size={24} />
              </div>
            </div>
            <h3 className="text-lg font-semibold text-slate-500">Lớp học đang mở</h3>
            <div className="mt-2 flex items-baseline">
              <p className="text-5xl font-bold text-purple-600">{stats.activeClasses}</p>
            </div>
          </div>
          
          <div className="bg-white p-6 rounded-2xl shadow-sm border border-slate-100 transform hover:-translate-y-1 transition-transform">
            <div className="flex items-center mb-4">
              <div className="p-3 bg-cyan-50 text-cyan-600 rounded-xl">
                <CalendarIcon size={24} />
              </div>
            </div>
            <h3 className="text-lg font-semibold text-slate-500">Buổi học hôm nay</h3>
            <div className="mt-2 flex items-baseline">
              <p className="text-5xl font-bold text-cyan-500">{stats.todaySchedules}</p>
            </div>
          </div>
        </div>
      )}

      {!loading && warningStudents.length > 0 && (
        <div className="bg-red-50 p-6 rounded-2xl shadow-sm border border-red-100 mt-8">
          <div className="flex items-center mb-4">
            <h3 className="text-xl font-bold text-red-700">Cảnh báo Học phí (Sắp hết buổi)</h3>
            <span className="ml-3 bg-red-600 text-white text-xs font-bold px-2 py-1 rounded-full">
              {warningStudents.length} học sinh
            </span>
          </div>
          <div className="bg-white rounded-xl shadow-sm overflow-x-auto">
            <table className="w-full text-left border-collapse min-w-[600px]">
              <thead>
                <tr className="bg-red-50/50 border-b border-red-100 text-red-600 text-sm">
                  <th className="p-4 font-semibold">Tên Học Sinh</th>
                  <th className="p-4 font-semibold">Tên Phụ Huynh</th>
                  <th className="p-4 font-semibold">Số điện thoại</th>
                  <th className="p-4 font-semibold text-center">Số buổi còn lại</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-red-50 text-sm">
                {warningStudents.map(student => (
                  <tr key={student.id} className="hover:bg-red-50/30 transition-colors">
                    <td className="p-4 font-medium text-slate-800">{student.fullName}</td>
                    <td className="p-4 text-slate-600">{student.parentName}</td>
                    <td className="p-4 font-bold text-slate-700">{student.parentPhoneNumber}</td>
                    <td className="p-4 text-center">
                      <span className="inline-block px-2.5 py-1 bg-red-100 text-red-700 rounded-lg font-bold">
                        {student.totalRemainingSessions}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
};

export default AdminDashboard;
