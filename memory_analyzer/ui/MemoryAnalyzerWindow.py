from PySide6.QtWidgets import (
    QMainWindow, QWidget, QVBoxLayout, QHBoxLayout,
    QLabel, QSplitter, QPushButton, QToolBar, QStatusBar,
    QFileDialog, QDockWidget
)
from PySide6.QtCore import Qt, Slot
from PySide6.QtGui import QAction
from typing import List

from core.stack_parser import StackParser
from core.allocation_parser import AllocationParser

from .StackTreeWidget import StackTreeWidget
from .FlameGraphWidget import FlameGraphWidget
from .TimelineWidget import TimelineWidget

class MemoryAnalyzerWindow(QMainWindow):
    """内存分析器主窗口"""
    
    def __init__(self):
        super().__init__()
        self.setWindowTitle("内存分析器")
        self.resize(1200, 800)
        
        # 数据解析器
        self.stack_parser = StackParser()
        self.alloc_parser = AllocationParser()
        
        self.setup_ui()
    
    def setup_ui(self):
        # 工具栏
        toolbar = QToolBar()
        self.addToolBar(toolbar)
        
        # 添加文件操作        
        load_all_action = QAction("加载文件", self)
        load_all_action.triggered.connect(self.load_all_file)
        toolbar.addAction(load_all_action)
        
        toolbar.addSeparator()
        
        # 添加视图切换
        self.view_flame = QAction("火焰图", self)
        self.view_flame.setCheckable(True)
        self.view_flame.setChecked(True)
        toolbar.addAction(self.view_flame)
        
        self.view_tree = QAction("调用树", self)
        self.view_tree.setCheckable(True)
        toolbar.addAction(self.view_tree)
        
        # 主分割窗口
        main_splitter = QSplitter(Qt.Vertical)

        # 上部分显示火焰图/调用树
        self.stack_view = QSplitter(Qt.Horizontal)
        self.flame_graph = FlameGraphWidget()
        self.stack_tree = StackTreeWidget()
        self.stack_view.addWidget(self.flame_graph)
        self.stack_view.addWidget(self.stack_tree)
        self.stack_tree.hide()  # 默认显示火焰图
        
        # 下部分显示时间线
        self.timeline = TimelineWidget()
        
        main_splitter.addWidget(self.stack_view)
        main_splitter.addWidget(self.timeline)
        
        # 设置分割比例为3:1
        total_height = 400  # 设置一个基准高度
        main_splitter.setSizes([total_height * 0.75, total_height * 0.25])  # 3:1比例
        
        self.setCentralWidget(main_splitter)
        
        # 设置视图切换事件
        self.view_flame.triggered.connect(self.toggle_view)
        self.view_tree.triggered.connect(self.toggle_view)
        
        # 设置数据联动
        self.timeline.frames_selected.connect(self.on_frames_selected)
        self.stack_tree.stack_selected.connect(self.on_stack_selected)
        self.flame_graph.stack_selected.connect(self.on_stack_selected)
        
        # 状态栏
        self.statusBar().showMessage("就绪")
    
    def toggle_view(self):
        """切换火焰图/调用树视图"""
        sender = self.sender()
        if sender == self.view_flame:
            self.view_tree.setChecked(not sender.isChecked())
            self.stack_tree.setVisible(not sender.isChecked())
            self.flame_graph.setVisible(sender.isChecked())
        else:  # view_tree
            self.view_flame.setChecked(not sender.isChecked())
            self.flame_graph.setVisible(not sender.isChecked())
            self.stack_tree.setVisible(sender.isChecked())
    
    def load_all_file(self):
        """加载信息文件"""

        has_error = False
        file_path, _ = QFileDialog.getOpenFileName(
            self, "选择堆栈文件", "", "Stack Files (*.txt);;All Files (*)"
        )
        if file_path:
            try:
                self.stack_parser.parse_file(file_path)
                self.statusBar().showMessage(f"已加载堆栈文件")
            except Exception as e:
                self.statusBar().showMessage(f"加载堆栈文件失败: {str(e)}")
                has_error = True

        file_path, _ = QFileDialog.getOpenFileName(
            self, "选择分配文件", "", "Allocation Files (*.txt);;All Files (*)"
        )
        if file_path:
            try:
                self.alloc_parser.parse_file(file_path)
                self.statusBar().showMessage(f"已加载分配文件")
            except Exception as e:
                self.statusBar().showMessage(f"加载分配文件失败: {str(e)}")
                has_error = True
        
        if has_error:
            self.statusBar().showMessage(f"加载文件失败")
            return
        self.update_views()
    
    def update_views(self):
        """更新所有视图"""
        # 更新堆栈视图
        traces = self.stack_parser.get_all_traces()
        allocations = {
            hash_id: self.alloc_parser.get_hash_allocations(hash_id)
            for hash_id in traces.keys()
        }
        self.stack_tree.update_data(traces, allocations)
        self.flame_graph.update_data(traces, allocations)
        
        # 更新时间线
        frame_data = {}
        min_frame, max_frame = self.alloc_parser.get_frame_range()
        for frame in range(min_frame, max_frame + 1):
            frame_data[frame] = self.alloc_parser.get_frame_total_size(frame)
        self.timeline.update_data(frame_data)
    
    @Slot(list)
    def on_frames_selected(self, frames: List[int]):
        """处理帧选择事件"""
        if not frames:
            self.statusBar().showMessage("未选择任何帧")
            return
            
        # 计算选中帧的总统计信息
        total_allocs = 0
        total_size = 0
        for frame in frames:
            allocations = self.alloc_parser.get_frame_allocations(frame)
            total_allocs += len(allocations)
            total_size += sum(a.size for a in allocations)
        
        # 显示统计信息
        if len(frames) == 1:
            self.statusBar().showMessage(
                f"帧 {frames[0]}: {total_allocs} 次分配, "
                f"总大小 {total_size/1024/1024:.1f} MB"
            )
        else:
            frame_range = f"{min(frames)}-{max(frames)}"
            self.statusBar().showMessage(
                f"帧范围 {frame_range}: {total_allocs} 次分配, "
                f"总大小 {total_size/1024/1024:.1f} MB, "
                f"平均每帧 {total_size/len(frames)/1024/1024:.1f} MB"
            )
    
    @Slot(str)
    def on_stack_selected(self, hash_id: str):
        """处理堆栈选择事件"""
        trace = self.stack_parser.get_trace(hash_id)
        allocations = self.alloc_parser.get_hash_allocations(hash_id)
        if trace and allocations:
            total_size = sum(a.size for a in allocations)
            self.statusBar().showMessage(
                f"堆栈 {hash_id}: {len(allocations)} 次分配, "
                f"总大小 {total_size/1024/1024:.1f} MB"
            ) 
