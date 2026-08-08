import React, { useState, useEffect } from 'react';
import { Calendar, Plus, Edit2, Trash2, Clock, CheckSquare } from 'lucide-react';
import axios from 'axios';
import Pagination from '../components/Pagination';

const ScheduleList = () => {
  const [schedules, setSchedules] = useState<any[]>([]);
  const [lectures, setLectures] = useState<any[]>([]);
  const [classes, setClasses] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

  const [showModal, setShowModal] = useState(false);
  const [editId, setEditId] = useState<number | null>(null);
  const [formData, setFormData] = useState<any>({
    userId: '',
    date: '',
    classIds: [],
    adminNotice: '',
    roomCode: '',
    tasks: [] as { taskName: string, isCompleted?: boolean }[]
  });

  const [currentPage, setCurrentPage] = useState(1);
  const [itemsPerPage, setItemsPerPage] = useState(25);

  const paginatedSchedules = React.useMemo(() => {
    const startIndex = (currentPage - 1) * itemsPerPage;
    return schedules.slice(startIndex, startIndex + itemsPerPage);
  }, [schedules, currentPage, itemsPerPage]);

  const resetForm = () => {
    setFormData({ userId: '', date: '', classIds: [], adminNotice: '', roomCode: '', tasks: [] });
    setEditId(null);
  };

  const fetchData = async () => {
    try {
      const token = localStorage.getItem('token');
      const [scheduleRes, lectureRes, classRes] = await Promise.all([
        axios.get(`/workschedule`, { headers: { Authorization: `Bearer ${token}` } }),
        axios.get(`/user/lectures`, { headers: { Authorization: `Bearer ${token}` } }),
        axios.get(`/class`, { headers: { Authorization: `Bearer ${token}` } })
      ]);
      setSchedules(scheduleRes.data);
      setLectures(lectureRes.data);
      setClasses(classRes.data);
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
    if (!window.confirm("Bạn có chắc muốn xóa lịch này?")) return;
    try {
      const token = localStorage.getItem('token');
      await axios.delete(`/workschedule/${id}`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      fetchData();
    } catch (error) {
      console.error("Lỗi xóa", error);
    }
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      const token = localStorage.getItem('token');
      const payload = {
        userId: formData.userId,
        date: new Date(formData.date).toISOString(),
        classIds: formData.classIds.map((id: string) => parseInt(id)),
        adminNotice: formData.adminNotice,
        roomCode: formData.roomCode,
        tasks: formData.tasks
      };
      if (editId) {
        await axios.put(`/workschedule/${editId}`, payload, {
          headers: { Authorization: `Bearer ${token}` }
        });
      } else {
        await axios.post(`/workschedule`, payload, {
          headers: { Authorization: `Bearer ${token}` }
        });
      }
      setShowModal(false);
      resetForm();
      fetchData();
    } catch (error) {
      console.error("Lỗi lưu lịch", error);
    }
  };

  const toggleClassSelection = (classId: number) => {
    setFormData((prev: any) => {
      const isSelected = prev.classIds.includes(classId);
      if (isSelected) {
        return { ...prev, classIds: prev.classIds.filter((id: number) => id !== classId) };
      } else {
        return { ...prev, classIds: [...prev.classIds, classId] };
      }
    });
  };

  return (
    <div className="space-y-6 animate-fade-in-up">
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 bg-white p-6 rounded-2xl shadow-sm border border-slate-100">
        <div>
          <h1 className="text-2xl font-bold text-slate-800">Phân công Lịch dạy</h1>
          <p className="text-slate-500 text-sm mt-1">Sắp xếp lịch làm việc cho Giáo viên (Lecture)</p>
        </div>
        <button 
          onClick={() => { resetForm(); setShowModal(true); }}
          className="w-full sm:w-auto flex items-center justify-center px-4 py-2 bg-indigo-600 text-white rounded-xl font-medium hover:bg-indigo-700 shadow-sm transition-colors"
        >
          <Plus size={18} className="mr-2" />
          Phân công mới
        </button>
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-slate-100 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse min-w-[800px]">
          <thead>
            <tr className="bg-slate-50/80 border-b border-slate-200 text-slate-500 text-xs uppercase tracking-wider">
              <th className="p-4 font-semibold rounded-tl-2xl w-32">Ngày làm việc</th>
              <th className="p-4 font-semibold w-48">Giáo viên (Lecture)</th>
              <th className="p-4 font-semibold w-64">Các lớp dạy</th>
              <th className="p-4 font-semibold">Ghi chú (Admin & Lecture)</th>
              <th className="p-4 font-semibold text-right rounded-tr-2xl">Hành động</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 text-sm">
            {paginatedSchedules.map((schedule) => (
              <tr key={schedule.id} className="hover:bg-slate-50 transition-colors group align-top">
                <td className="p-4 font-medium text-slate-800 whitespace-nowrap">
                  <div className="flex items-center">
                    <Calendar size={16} className="text-indigo-500 mr-2" />
                    {new Date(schedule.date).toLocaleDateString('vi-VN')}
                  </div>
                  <div className="mt-2 text-xs">
                    {schedule.isTaskCompleted ? (
                      <span className="inline-flex items-center text-emerald-600 bg-emerald-50 px-2 py-1 rounded-md font-semibold border border-emerald-100">
                        <CheckSquare size={12} className="mr-1" /> Đã hoàn thành
                      </span>
                    ) : (
                      <span className="inline-flex items-center text-amber-600 bg-amber-50 px-2 py-1 rounded-md font-semibold border border-amber-100">
                        <Clock size={12} className="mr-1" /> Chờ thực hiện
                      </span>
                    )}
                  </div>
                </td>
                <td className="p-4">
                  <span className="inline-flex items-center px-2.5 py-1 rounded-lg text-xs font-medium bg-emerald-50 text-emerald-700 border border-emerald-100">
                    {schedule.lectureName || 'N/A'}
                  </span>
                </td>
                <td className="p-4 text-slate-600">
                  <div className="space-y-2">
                    {schedule.classes && schedule.classes.map((c: any) => (
                      <div key={c.id} className="bg-white p-2 border border-slate-200 rounded-lg shadow-sm">
                        <p className="font-bold text-slate-800 text-xs">{c.className}</p>
                        <p className="text-xs text-slate-500 mt-0.5">{c.timeSlots || 'Không có giờ'}</p>
                      </div>
                    ))}
                    {schedule.roomCode && (
                      <div className="text-xs text-slate-500 mt-1 font-medium">Phòng: {schedule.roomCode}</div>
                    )}
                  </div>
                </td>
                <td className="p-4 text-slate-600">
                  <div className="grid grid-cols-2 gap-2 text-xs">
                    <div className="bg-blue-50/50 p-2 rounded-lg border border-blue-100">
                      <p className="font-bold text-blue-800 mb-1">Ghi chú & Nhiệm vụ:</p>
                      <p className="text-slate-600 whitespace-pre-wrap">{schedule.adminNotice || '---'}</p>
                      {schedule.tasks && schedule.tasks.length > 0 && (
                        <div className="mt-2 space-y-1">
                          {schedule.tasks.map((t: any) => (
                            <div key={t.id} className="flex items-center text-xs">
                              {t.isCompleted ? <CheckSquare size={12} className="text-emerald-500 mr-1" /> : <div className="w-3 h-3 border border-slate-300 rounded-sm mr-1"></div>}
                              <span className={t.isCompleted ? "text-indigo-600 font-semibold" : "text-slate-700"}>{t.taskName}</span>
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                    <div className="bg-purple-50/50 p-2 rounded-lg border border-purple-100">
                      <p className="font-bold text-purple-800 mb-1">Lecture Notice:</p>
                      <p className="text-slate-600 whitespace-pre-wrap">{schedule.lectureNotice || '---'}</p>
                    </div>
                  </div>
                </td>
                <td className="p-4 text-right whitespace-nowrap">
                  <div className="flex items-center justify-end space-x-2">
                    <button 
                      onClick={() => {
                        setEditId(schedule.id);
                        setFormData({
                          userId: schedule.lectureId,
                          date: new Date(schedule.date).toISOString().split('T')[0],
                          classIds: schedule.classes ? schedule.classes.map((c: any) => c.id) : [],
                          adminNotice: schedule.adminNotice || '',
                          roomCode: schedule.roomCode || '',
                          tasks: schedule.tasks ? schedule.tasks.map((t: any) => ({ taskName: t.taskName, isCompleted: t.isCompleted })) : []
                        });
                        setShowModal(true);
                      }}
                      className="p-1.5 text-indigo-600 hover:bg-indigo-50 rounded-lg transition-colors"
                    >
                      <Edit2 size={16} />
                    </button>
                    <button onClick={() => handleDelete(schedule.id)} className="p-1.5 text-red-600 hover:bg-red-50 rounded-lg transition-colors">
                      <Trash2 size={16} />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
            {schedules.length === 0 && (
              <tr>
                <td colSpan={5} className="p-8 text-center text-slate-500">Chưa có lịch phân công nào.</td>
              </tr>
            )}
          </tbody>
        </table>
        </div>
        
        <Pagination 
          totalItems={schedules.length}
          itemsPerPage={itemsPerPage}
          currentPage={currentPage}
          onPageChange={setCurrentPage}
          onItemsPerPageChange={(items) => {
            setItemsPerPage(items);
            setCurrentPage(1);
          }}
        />
      </div>

      {showModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg flex flex-col max-h-[90vh]">
            <div className="p-6 border-b border-slate-100 flex justify-between items-center">
              <h3 className="text-xl font-bold text-slate-800">{editId ? 'Sửa phân công' : 'Phân công lịch mới'}</h3>
              <button onClick={() => setShowModal(false)} className="text-slate-400 hover:text-slate-600 font-bold">Đóng</button>
            </div>
            <div className="p-6 overflow-y-auto">
              <form onSubmit={handleSave} className="space-y-4">
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-medium text-slate-700 mb-1">Giáo viên</label>
                    <select 
                      required
                      value={formData.userId}
                      onChange={e => setFormData({...formData, userId: e.target.value})}
                      className="w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50"
                    >
                      <option value="">-- Chọn --</option>
                      {lectures.map(l => (
                        <option key={l.id} value={l.id}>{l.fullName}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-slate-700 mb-1">Ngày dạy</label>
                    <input 
                      type="date" required
                      value={formData.date}
                      onChange={e => setFormData({...formData, date: e.target.value})}
                      className="w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50"
                    />
                  </div>
                </div>

                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Phòng (Room Code)</label>
                  <input 
                    type="text"
                    placeholder="VD: P101"
                    value={formData.roomCode}
                    onChange={e => setFormData({...formData, roomCode: e.target.value})}
                    className="w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50"
                  />
                </div>

                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-2">Chọn Lớp học trong ca này</label>
                  <div className="max-h-48 overflow-y-auto border border-slate-200 rounded-xl p-2 space-y-1 bg-slate-50/50">
                    {classes.map(c => (
                      <label key={c.id} className="flex items-start p-2 hover:bg-white rounded-lg cursor-pointer border border-transparent hover:border-slate-200 transition-all">
                        <input 
                          type="checkbox" 
                          checked={formData.classIds.includes(c.id)}
                          onChange={() => toggleClassSelection(c.id)}
                          className="mt-1 w-4 h-4 text-indigo-600 rounded border-slate-300 focus:ring-indigo-500"
                        />
                        <div className="ml-3">
                          <p className="font-bold text-slate-800 text-sm">{c.className}</p>
                          <p className="text-xs text-slate-500">{c.timeSlots || 'Chưa thiết lập giờ'}</p>
                        </div>
                      </label>
                    ))}
                  </div>
                </div>

                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Ghi chú cho Lecture (Admin Notice)</label>
                  <textarea 
                    rows={2}
                    placeholder="VD: Chú ý ôn tập chương 2..."
                    value={formData.adminNotice}
                    onChange={e => setFormData({...formData, adminNotice: e.target.value})}
                    className="w-full border border-slate-200 rounded-xl px-4 py-2 focus:ring-2 focus:ring-indigo-500/50"
                  />
                </div>

                <div>
                  <div className="flex items-center justify-between mb-2">
                    <label className="block text-sm font-medium text-slate-700">Nhiệm vụ cụ thể (Tasks)</label>
                    <button
                      type="button"
                      onClick={() => setFormData((prev: any) => ({ ...prev, tasks: [...prev.tasks, { taskName: '' }] }))}
                      className="text-xs font-semibold text-indigo-600 hover:text-indigo-800 bg-indigo-50 px-2 py-1 rounded-md"
                    >
                      + Thêm Task
                    </button>
                  </div>
                  <div className="space-y-2">
                    {formData.tasks.map((task, index) => (
                      <div key={index} className="flex items-center space-x-2">
                        <input
                          type="text"
                          required
                          placeholder={`Nhiệm vụ ${index + 1}`}
                          value={task.taskName}
                          onChange={(e) => {
                            const newTasks = [...formData.tasks];
                            newTasks[index].taskName = e.target.value;
                            setFormData({ ...formData, tasks: newTasks });
                          }}
                          className="flex-1 border border-slate-200 rounded-xl px-4 py-2 text-sm focus:ring-2 focus:ring-indigo-500/50"
                        />
                        <button
                          type="button"
                          onClick={() => {
                            const newTasks = formData.tasks.filter((_, i) => i !== index);
                            setFormData({ ...formData, tasks: newTasks });
                          }}
                          className="p-2 text-red-500 hover:bg-red-50 rounded-lg"
                        >
                          <Trash2 size={16} />
                        </button>
                      </div>
                    ))}
                    {formData.tasks.length === 0 && (
                      <p className="text-xs text-slate-400 italic">Chưa có nhiệm vụ cụ thể nào. Giáo viên sẽ chỉ thấy ghi chú chung.</p>
                    )}
                  </div>
                </div>

                <div className="flex justify-end space-x-3 pt-4 border-t border-slate-100">
                  <button type="button" onClick={() => setShowModal(false)} className="px-4 py-2 text-slate-600 hover:bg-slate-100 rounded-xl font-medium">Hủy</button>
                  <button type="submit" className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl font-medium shadow-sm">Lưu phân công</button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default ScheduleList;
