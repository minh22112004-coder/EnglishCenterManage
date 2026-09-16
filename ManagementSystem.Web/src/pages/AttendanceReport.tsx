import React, { useState, useEffect, useRef } from 'react';
import { Search, Download, Filter, FileText } from 'lucide-react';
import axios from 'axios';
import * as xlsx from 'xlsx';
import Pagination from '../components/Pagination';

const AttendanceReport = () => {
  const [reports, setReports] = useState<any[]>([]);
  const [filteredReports, setFilteredReports] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  
  // Filters
  const [searchName, setSearchName] = useState('');
  const [searchPhone, setSearchPhone] = useState('');
  const [searchClass, setSearchClass] = useState('');
  const [searchMonth, setSearchMonth] = useState(''); // Mặc định hiển thị tất cả
  
  const [currentPage, setCurrentPage] = useState(1);
  const [itemsPerPage, setItemsPerPage] = useState(25);
  const hasFetchedRef = useRef(false);

  const paginatedReports = React.useMemo(() => {
    const startIndex = (currentPage - 1) * itemsPerPage;
    return filteredReports.slice(startIndex, startIndex + itemsPerPage);
  }, [filteredReports, currentPage, itemsPerPage]);

  useEffect(() => {
    setCurrentPage(1);
  }, [searchName, searchPhone, searchClass, searchMonth]);

  const fetchReports = async () => {
    try {
      setLoading(true);
      const token = localStorage.getItem('token');
      const response = await axios.get(`/attendance/report`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      setReports(response.data);
      setFilteredReports(response.data);
    } catch (error) {
      console.error("Lỗi lấy báo cáo", error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (hasFetchedRef.current) return;
    hasFetchedRef.current = true;
    fetchReports();
  }, []);

  useEffect(() => {
    let result = reports;
    if (searchName) {
      result = result.filter(r => r.fullName.toLowerCase().includes(searchName.toLowerCase()) || r.parentName.toLowerCase().includes(searchName.toLowerCase()));
    }
    if (searchPhone) {
      result = result.filter(r => r.parentPhoneNumber.includes(searchPhone));
    }
    if (searchClass) {
      result = result.filter(r => r.className.toLowerCase().includes(searchClass.toLowerCase()));
    }

    // Sort by className (which corresponds to ClassId visually)
    result = [...result].sort((a, b) => a.className.localeCompare(b.className));

    const todayStr = new Date().toISOString().split('T')[0];

    // Filter dates within each report
    result = result.map(r => {
      let presentDates = r.presentDates;
      let absentDates = r.absentDates;

      if (searchMonth) {
        presentDates = presentDates.filter((d: string) => d.startsWith(searchMonth));
        absentDates = absentDates.filter((d: string) => d.startsWith(searchMonth));
      }

      // Không hiển thị ngày vắng mặt trong tương lai
      absentDates = absentDates.filter((d: string) => d.split('T')[0] <= todayStr);

      return { ...r, presentDates, absentDates };
    });

    setFilteredReports(result);
  }, [searchName, searchPhone, searchClass, searchMonth, reports]);

  const handleExportExcel = () => {
    const dataToExport = filteredReports.map(r => ({
      "Học sinh": r.fullName,
      "Phụ huynh": r.parentName,
      "Số điện thoại": r.parentPhoneNumber,
      "Lớp": r.className,
      "Các ngày Có mặt": r.presentDates.map((d: string) => new Date(d).toLocaleDateString('vi-VN')).join(', ')
    }));

    const ws = xlsx.utils.json_to_sheet(dataToExport);
    const wb = xlsx.utils.book_new();
    xlsx.utils.book_append_sheet(wb, ws, "BaoCaoDiemDanh");
    xlsx.writeFile(wb, `BaoCaoDiemDanh_${new Date().toISOString().split('T')[0]}.xlsx`);
  };

  return (
    <div className="space-y-6 animate-fade-in-up">
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 bg-white p-6 rounded-2xl shadow-sm border border-slate-100">
        <div>
          <h1 className="text-2xl font-bold text-slate-800">Báo cáo Điểm danh</h1>
          <p className="text-slate-500 text-sm mt-1">Xem lịch sử đi học của học sinh và xuất file</p>
        </div>
        <button 
          onClick={handleExportExcel}
          className="w-full sm:w-auto flex items-center justify-center px-4 py-2 bg-emerald-600 text-white rounded-xl font-medium hover:bg-emerald-700 shadow-sm transition-colors"
        >
          <Download size={18} className="mr-2" />
          Xuất Excel
        </button>
      </div>

      <div className="bg-white p-6 rounded-2xl shadow-sm border border-slate-100">
        <div className="flex items-center text-slate-800 font-bold mb-4">
          <Filter size={18} className="mr-2 text-indigo-600" />
          Bộ lọc dữ liệu
        </div>
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-slate-400" size={18} />
            <input 
              type="text" 
              placeholder="Tìm theo Tên HS hoặc Phụ huynh..." 
              value={searchName}
              onChange={(e) => setSearchName(e.target.value)}
              className="w-full pl-10 pr-4 py-2 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-indigo-500/50 text-sm"
            />
          </div>
          <div>
            <input 
              type="text" 
              placeholder="Tìm theo Lớp..." 
              value={searchClass}
              onChange={(e) => setSearchClass(e.target.value)}
              className="w-full px-4 py-2 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-indigo-500/50 text-sm"
            />
          </div>
          <div>
            <input 
              type="text" 
              placeholder="Tìm theo Số điện thoại..." 
              value={searchPhone}
              onChange={(e) => setSearchPhone(e.target.value)}
              className="w-full px-4 py-2 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-indigo-500/50 text-sm"
            />
          </div>
          <div>
            <input 
              type="month" 
              value={searchMonth}
              onChange={(e) => setSearchMonth(e.target.value)}
              className="w-full px-4 py-2 border border-slate-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-indigo-500/50 text-sm text-slate-600"
            />
          </div>
        </div>
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-slate-100 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse min-w-[1000px]">
            <thead>
              <tr className="bg-slate-50/80 border-b border-slate-200 text-slate-500 text-xs uppercase tracking-wider">
                <th className="p-4 font-semibold w-1/4">Tên Học Sinh</th>
                <th className="p-4 font-semibold w-1/6">Lớp</th>
                <th className="p-4 font-semibold w-1/2 text-emerald-700">Các ngày Có mặt</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 text-sm">
              {loading ? (
                <tr>
                  <td colSpan={3} className="p-8 text-center text-slate-500">Đang tải dữ liệu...</td>
                </tr>
              ) : filteredReports.length === 0 ? (
                <tr>
                  <td colSpan={3} className="p-8 text-center text-slate-500">
                    <FileText size={32} className="mx-auto mb-2 text-slate-300" />
                    Không tìm thấy dữ liệu nào.
                  </td>
                </tr>
              ) : (
                paginatedReports.map((r) => (
                  <tr key={r.studentId} className="hover:bg-slate-50/80 transition-colors">
                    <td className="p-4">
                      <p className="font-bold text-slate-800">{r.fullName}</p>
                      <p className="text-xs text-slate-500 mt-0.5">PH: {r.parentName}</p>
                      <p className="text-xs text-slate-500 mt-0.5">SĐT: {r.parentPhoneNumber}</p>
                    </td>
                    <td className="p-4 font-medium text-indigo-700">{r.className}</td>
                    <td className="p-4">
                      <div className="flex flex-wrap gap-1.5">
                        {r.presentDates.length === 0 ? (
                          <span className="text-xs text-slate-400 italic">Chưa có dữ liệu</span>
                        ) : (
                          r.presentDates.map((d: string, idx: number) => (
                            <span key={idx} className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium bg-emerald-50 text-emerald-700 border border-emerald-100">
                              {new Date(d).toLocaleDateString('vi-VN')}
                            </span>
                          ))
                        )}
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
        
        <Pagination 
          totalItems={filteredReports.length}
          itemsPerPage={itemsPerPage}
          currentPage={currentPage}
          onPageChange={setCurrentPage}
          onItemsPerPageChange={(items) => {
            setItemsPerPage(items);
            setCurrentPage(1);
          }}
        />
      </div>
    </div>
  );
};

export default AttendanceReport;
