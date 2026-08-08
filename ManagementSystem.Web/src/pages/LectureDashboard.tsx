import React, { useState, useEffect } from 'react';
import { Calendar as CalendarIcon, CheckCircle, Users, Clock, AlertCircle, Save, CheckSquare, Square, FileText, BarChart2 } from 'lucide-react';
import axios from 'axios';

const LectureDashboard = () => {
  const [schedules, setSchedules] = useState<any[]>([]);
  const [students, setStudents] = useState<any[]>([]);
  const [selectedDate, setSelectedDate] = useState(new Date().toISOString().split('T')[0]);
  const [viewMode, setViewMode] = useState<'day' | 'week' | 'month'>('day');
  const [loading, setLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  
  const [selectedSchedule, setSelectedSchedule] = useState<any | null>(null);
  
  // Trạng thái điểm danh (StudentId -> IsPresent)
  const [attendanceState, setAttendanceState] = useState<Record<number, boolean>>({});
  const [lectureNotice, setLectureNotice] = useState('');
  const [tasksState, setTasksState] = useState<any[]>([]);

  const fetchData = async () => {
    try {
      setLoading(true);
      const token = localStorage.getItem('token');
      
      const [scheduleRes, studentRes] = await Promise.all([
        axios.get(`/workschedule`, { headers: { Authorization: `Bearer ${token}` } }),
        axios.get(`/student`, { headers: { Authorization: `Bearer ${token}` } })
      ]);
      
      setSchedules(scheduleRes.data);
      setStudents(studentRes.data);
    } catch (error) {
      console.error("Lỗi lấy dữ liệu", error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, []);

  const filteredSchedules = schedules.filter(s => {
    const sDate = new Date(s.date);
    sDate.setHours(0, 0, 0, 0);
    const selected = new Date(selectedDate);
    selected.setHours(0, 0, 0, 0);

    if (viewMode === 'day') {
      return sDate.getTime() === selected.getTime();
    } else if (viewMode === 'week') {
      const day = selected.getDay();
      const diff = selected.getDate() - day + (day === 0 ? -6 : 1);
      const startOfWeek = new Date(selected);
      startOfWeek.setDate(diff);
      const endOfWeek = new Date(startOfWeek);
      endOfWeek.setDate(startOfWeek.getDate() + 6);
      return sDate >= startOfWeek && sDate <= endOfWeek;
    } else if (viewMode === 'month') {
      return sDate.getMonth() === selected.getMonth() && sDate.getFullYear() === selected.getFullYear();
    }
    return false;
  }).sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime());

  // Calculate total classes taught this month
  const currentMonth = new Date().getMonth();
  const currentYear = new Date().getFullYear();
  const schedulesThisMonth = schedules.filter(s => {
    const sDate = new Date(s.date);
    const isThisMonth = sDate.getMonth() === currentMonth && sDate.getFullYear() === currentYear;
    // TÍNH CÔNG: Chỉ tính những ca đã dạy (ngày trong quá khứ hoặc đã đánh dấu hoàn thành)
    const hasTaught = sDate.getTime() <= new Date().getTime() || s.isTaskCompleted;
    return isThisMonth && hasTaught;
  });
  const totalClassesThisMonth = schedulesThisMonth.reduce((acc, curr) => acc + (curr.classes?.length || 0), 0);

  const handleSelectSchedule = async (schedule: any) => {
    setSelectedSchedule(schedule);
    setLectureNotice(schedule.lectureNotice || '');
    setTasksState(schedule.tasks || []);
    
    // Fetch attendance for this date
    try {
      const token = localStorage.getItem('token');
      const dateStr = new Date(schedule.date).toISOString().split('T')[0];
      const res = await axios.get(`/attendance/date/${dateStr}`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      
      const newAttendanceState: Record<number, boolean> = {};
      res.data.forEach((att: any) => {
        newAttendanceState[att.studentId] = att.isPresent;
      });
      setAttendanceState(newAttendanceState);
    } catch (e) {
      console.error("Lỗi lấy điểm danh:", e);
      setAttendanceState({});
    }
  };

  const handleCheckboxChange = (studentId: number) => {
    setAttendanceState((prev: any) => ({
      ...prev,
      [studentId]: !prev[studentId]
    }));
  };

  const handleSaveAll = async () => {
    if (!selectedSchedule) return;
    setIsSaving(true);

    try {
      const token = localStorage.getItem('token');
      
      // 1. Lưu điểm danh (Chỉ gửi những gì có trong attendanceState)
      // Lấy danh sách student hiện tại của ca dạy
      const dtos = currentStudents.map(student => {
        // Nếu không check thì mặc định false
        const isPresent = attendanceState[student.id] || false;
        return {
          studentId: student.id,
          date: new Date(selectedDate).toISOString(),
          isPresent: isPresent
        };
      });

      if (dtos.length > 0) {
        await axios.post(`/attendance/batch`, dtos, {
          headers: { Authorization: `Bearer ${token}` }
        });
      }

      // 2. Cập nhật Notice và Todo
      await axios.put(`/workschedule/${selectedSchedule.id}`, {
        userId: "lecture_update", // Dummy để qua validate DTO nếu required
        date: selectedSchedule.date || new Date().toISOString(),
        classIds: [], // Dummy
        adminNotice: selectedSchedule.adminNotice || "",
        lectureNotice: lectureNotice,
        tasks: tasksState
      }, {
        headers: { Authorization: `Bearer ${token}` }
      });

      alert("Lưu tất cả thành công!");
      
      // Update local state without triggering a heavy network reload
      setSchedules(prevSchedules => prevSchedules.map(s => 
        s.id === selectedSchedule.id 
          ? { ...s, lectureNotice, tasks: tasksState, isTaskCompleted: tasksState.every(t => t.isCompleted) }
          : s
      ));
      
      // Update selected schedule state implicitly by waiting for next click, or just update it manually
      setSelectedSchedule(prev => ({...prev, lectureNotice, tasks: tasksState}));

    } catch (error) {
      console.error("Lỗi lưu", error);
      alert("Lưu thất bại. Kiểm tra kết nối mạng.");
    } finally {
      setIsSaving(false);
    }
  };

  // Nếu đã chọn 1 ca dạy, lấy ra danh sách classIds
  const currentClassIds = selectedSchedule?.classes?.map((c:any) => c.id) || [];
  // Lọc học sinh thuộc các lớp trong ca dạy đó
  const currentStudents = students.filter(s => currentClassIds.includes(s.classId));

  return (
    <div className="space-y-6 animate-fade-in-up">
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-white p-6 rounded-2xl shadow-sm border border-slate-100">
        <div>
          <h1 className="text-3xl font-bold text-slate-800 tracking-tight">Khu vực Giáo Viên</h1>
          <p className="text-slate-500 mt-1">Đang hiển thị {filteredSchedules.length} ca dạy.</p>
        </div>
        
        <div className="flex items-center space-x-6">
          <div className="flex items-center space-x-3 bg-indigo-50/50 p-3 rounded-xl border border-indigo-100">
            <BarChart2 className="text-indigo-500" size={24} />
            <div>
              <p className="text-xs text-indigo-600 font-bold uppercase tracking-wider">Tháng {currentMonth + 1}</p>
              <p className="text-sm font-bold text-slate-800"><span className="text-xl text-indigo-700">{totalClassesThisMonth}</span> lớp đã dạy</p>
            </div>
          </div>
          <div className="flex items-center space-x-3">
            <select
              value={viewMode}
              onChange={(e) => {
                setViewMode(e.target.value as any);
                setSelectedSchedule(null);
                setAttendanceState({});
              }}
              className="pl-4 pr-8 py-2 border border-slate-200 rounded-xl focus:ring-2 focus:ring-indigo-500/50 bg-white shadow-sm font-medium text-slate-700"
            >
              <option value="day">Theo Ngày</option>
              <option value="week">Theo Tuần</option>
              <option value="month">Theo Tháng</option>
            </select>
            <input 
              type="date" 
              value={selectedDate}
              onChange={(e) => {
                setSelectedDate(e.target.value);
                setSelectedSchedule(null); // Reset when changing date
                setAttendanceState({});
              }}
              className="pl-4 pr-4 py-2 border border-slate-200 rounded-xl focus:ring-2 focus:ring-indigo-500/50 bg-white shadow-sm font-medium"
            />
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
        
        {/* Danh sách Ca dạy (Left Column) */}
        <div className="lg:col-span-1 space-y-4">
          <h2 className="text-lg font-bold text-slate-800 mb-2 flex items-center">
            <Clock size={18} className="mr-2 text-indigo-600" /> Danh sách ca dạy ({viewMode === 'day' ? 'Ngày' : viewMode === 'week' ? 'Tuần' : 'Tháng'})
          </h2>
          
          {filteredSchedules.map((schedule) => (
            <div 
              key={schedule.id} 
              onClick={() => handleSelectSchedule(schedule)}
              className={`p-4 rounded-2xl border-2 transition-all cursor-pointer ${
                selectedSchedule?.id === schedule.id 
                  ? 'bg-indigo-50 border-indigo-500 shadow-md transform scale-[1.02]' 
                  : 'bg-white border-slate-100 hover:border-indigo-200 hover:shadow-sm'
              }`}
            >
              <div className="flex justify-between items-start mb-2">
                <span className="text-xs font-semibold text-indigo-600 bg-indigo-50 px-2 py-1 rounded">
                  {new Date(schedule.date).toLocaleDateString('vi-VN')}
                </span>
                <span className={`inline-flex items-center px-2 py-1 rounded text-[10px] font-bold uppercase tracking-wider ${schedule.tasks && schedule.tasks.length > 0 && schedule.tasks.every((t:any) => t.isCompleted) ? 'bg-emerald-100 text-emerald-700' : 'bg-amber-100 text-amber-700'}`}>
                  {schedule.tasks && schedule.tasks.length > 0 && schedule.tasks.every((t:any) => t.isCompleted) ? 'Đã hoàn thành' : 'Chưa hoàn thành'}
                </span>
              </div>
              <h3 className="font-bold text-slate-800 text-sm mb-1">
                {schedule.classes?.length} Lớp học
              </h3>
              <div className="space-y-1 mt-2">
                {schedule.classes?.map((c:any) => (
                  <p key={c.id} className="text-xs text-slate-600 font-medium">• {c.className}</p>
                ))}
              </div>
            </div>
          ))}
          {filteredSchedules.length === 0 && (
            <div className="bg-white p-6 rounded-2xl border border-slate-100 text-center text-slate-500 shadow-sm">
              <CalendarIcon size={32} className="mx-auto mb-2 text-slate-300" />
              <p>Không có ca dạy nào.</p>
            </div>
          )}
        </div>

        {/* Chi tiết Ca dạy (Right Area) */}
        <div className="lg:col-span-3">
          {!selectedSchedule ? (
            <div className="bg-white rounded-3xl shadow-sm border border-slate-100 h-full min-h-[400px] flex items-center justify-center text-slate-400">
              <div className="text-center">
                <FileText size={48} className="mx-auto mb-4 text-slate-200" />
                <p className="text-lg">Chọn một ca dạy bên trái để thao tác</p>
              </div>
            </div>
          ) : (
            <div className="bg-white rounded-3xl shadow-sm border border-slate-100 flex flex-col overflow-hidden animate-fade-in-up">
              {/* Header ca dạy */}
              <div className="bg-slate-800 p-6 text-white flex justify-between items-center">
                <div>
                  <h2 className="text-xl font-bold">Chi tiết Ca dạy</h2>
                  <div className="text-slate-400 mt-2 flex flex-wrap gap-4 text-sm">
                        <span className="flex items-center">
                          <Clock size={14} className="mr-1" /> {selectedSchedule.classes.map((c: any) => c.timeSlots).join(', ')}
                        </span>
                        {selectedSchedule.roomCode && (
                          <span className="flex items-center font-medium text-indigo-400">
                            📍 Phòng: {selectedSchedule.roomCode}
                          </span>
                        )}
                  </div>
                </div>
                <button 
                  onClick={handleSaveAll}
                  disabled={isSaving}
                  className={`flex items-center px-6 py-2.5 font-bold rounded-xl shadow transition-all transform hover:-translate-y-0.5 ${isSaving ? 'bg-slate-400 text-white cursor-not-allowed' : 'bg-emerald-500 text-white hover:bg-emerald-600'}`}
                >
                  <Save size={18} className={`mr-2 ${isSaving ? 'animate-bounce' : ''}`} />
                  {isSaving ? 'Đang lưu...' : 'Lưu Tất Cả'}
                </button>
              </div>

              <div className="p-6 grid grid-cols-1 md:grid-cols-2 gap-6 bg-slate-50 border-b border-slate-100">
                {/* Notice Admin */}
                <div className="bg-white p-4 rounded-2xl border border-blue-100 shadow-sm relative overflow-hidden">
                  <div className="absolute top-0 left-0 w-1 h-full bg-blue-500"></div>
                  <h3 className="font-bold text-blue-900 text-sm mb-2 uppercase tracking-wide">Notice từ Admin</h3>
                  <div className="p-3 bg-blue-50/50 rounded-xl text-slate-700 text-sm min-h-[100px] whitespace-pre-wrap">
                    {selectedSchedule.adminNotice || 'Không có ghi chú nào từ Admin.'}
                  </div>
                </div>

                {/* Notice Lecture */}
                <div className="bg-white p-4 rounded-2xl border border-purple-100 shadow-sm relative overflow-hidden flex flex-col">
                  <div className="absolute top-0 left-0 w-1 h-full bg-purple-500"></div>
                  <h3 className="font-bold text-purple-900 text-sm mb-2 uppercase tracking-wide">Sổ tay Lecture (Tự ghi)</h3>
                  <textarea 
                    value={lectureNotice}
                    onChange={e => setLectureNotice(e.target.value)}
                    placeholder="Ghi chú cá nhân cho ca dạy này..."
                    className="flex-1 w-full p-3 bg-purple-50/50 rounded-xl text-slate-700 text-sm border-0 focus:ring-2 focus:ring-purple-300 resize-none"
                  ></textarea>
                </div>
              </div>

              {/* Task Todo & Điểm danh */}
              <div className="p-6">
                <div className="mb-6">
                  <h3 className="text-lg font-bold text-slate-800 flex items-center mb-4">
                    <CheckSquare size={20} className="mr-2 text-indigo-600" /> Nhiệm vụ cần hoàn thành (Tasks)
                  </h3>
                  {tasksState.length > 0 ? (
                    <div className="space-y-2 bg-slate-50 p-4 rounded-xl border border-slate-200">
                      {tasksState.map((task, index) => (
                        <label key={task.id || index} className="flex items-center p-2 hover:bg-white rounded-lg cursor-pointer transition-colors">
                          <input 
                            type="checkbox" 
                            checked={task.isCompleted}
                            onChange={(e) => {
                              const newTasks = [...tasksState];
                              newTasks[index].isCompleted = e.target.checked;
                              setTasksState(newTasks);
                            }}
                            className="w-5 h-5 text-indigo-600 rounded border-gray-300 focus:ring-indigo-500 mr-3"
                          />
                          <span className={`font-medium text-sm ${task.isCompleted ? 'text-indigo-600 font-semibold' : 'text-slate-700'}`}>
                          {task.taskName}
                        </span>
                        </label>
                      ))}
                    </div>
                  ) : (
                    <div className="p-4 bg-slate-50 rounded-xl border border-slate-200 text-slate-500 text-sm italic">
                      Không có nhiệm vụ cụ thể nào được giao cho ca này.
                    </div>
                  )}
                </div>

                <div className="flex items-center justify-between mb-6">
                  <h3 className="text-lg font-bold text-slate-800 flex items-center">
                    <Users size={20} className="mr-2 text-indigo-600" /> Điểm danh Học sinh
                  </h3>
                </div>

                <div className="border border-slate-200 rounded-2xl overflow-hidden">
                  <table className="w-full text-left">
                    <thead className="bg-slate-50">
                      <tr>
                        <th className="p-4 font-semibold text-slate-600 text-sm">Học sinh</th>
                        <th className="p-4 font-semibold text-slate-600 text-sm text-center">Trạng thái</th>
                        <th className="p-4 font-semibold text-slate-600 text-sm text-right">Thao tác</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {currentStudents.map(student => {
                        const isChecked = attendanceState[student.id] || false;
                        return (
                          <tr key={student.id} className="hover:bg-slate-50/50 transition-colors">
                            <td className="p-4">
                              <p className="font-bold text-slate-800">{student.fullName}</p>
                              <p className="text-xs text-slate-500">Lớp ID: {student.classId}</p>
                            </td>
                            <td className="p-4 text-center">
                              {student.totalRemainingSessions < 3 ? (
                                <span className="inline-flex items-center px-2 py-1 rounded-md text-[10px] font-bold bg-red-100 text-red-700">
                                  <AlertCircle size={10} className="mr-1" /> Sắp hết ({student.totalRemainingSessions})
                                </span>
                              ) : (
                                <span className="text-xs font-medium text-slate-500">Còn {student.totalRemainingSessions} buổi</span>
                              )}
                            </td>
                            <td className="p-4 text-right">
                              <label className="inline-flex items-center cursor-pointer">
                                <input 
                                  type="checkbox" 
                                  checked={isChecked}
                                  onChange={() => handleCheckboxChange(student.id)}
                                  className="w-5 h-5 text-emerald-500 bg-gray-100 border-gray-300 rounded focus:ring-emerald-500 cursor-pointer"
                                />
                                <span className="ml-2 text-sm font-medium text-slate-600 select-none">Có mặt</span>
                              </label>
                            </td>
                          </tr>
                        );
                      })}
                      {currentStudents.length === 0 && (
                        <tr>
                          <td colSpan={3} className="p-8 text-center text-slate-500">Ca dạy này chưa có học sinh nào.</td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>
              </div>

            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default LectureDashboard;
