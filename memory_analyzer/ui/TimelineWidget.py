from PySide6.QtWidgets import QWidget, QVBoxLayout, QApplication
from PySide6.QtCore import Qt, Signal
import pyqtgraph as pg
import numpy as np
from typing import Dict, List, Optional, Tuple, Set
from core.stack_parser import StackTrace
from core.allocation_parser import AllocationRecord


class TimelineWidget(QWidget):
    """时间线视图"""
    
    frames_selected = Signal(list)  # 发送选中的帧号列表
    
    def __init__(self, parent=None):
        super().__init__(parent)
        self.layout = QVBoxLayout(self)
        
        # 创建pyqtgraph视图
        self.view = pg.GraphicsLayoutWidget()
        self.view.setBackground('white')
        self.layout.addWidget(self.view)
        
        # 创建绘图区域
        self.plot = self.view.addPlot()
        self.plot.setLabel('left', '内存分配大小 (MB)')
        self.plot.setLabel('bottom', '帧号')
        self.plot.showGrid(x=True, y=True, alpha=0.3)
        
        # 创建散点图
        self.scatter = pg.ScatterPlotItem(
            size=8,
            pen=pg.mkPen(None),
            brush=pg.mkBrush(255, 0, 0, 120),
            hoverable=True,
            hoverBrush=pg.mkBrush(255, 0, 0, 200)
        )
        self.plot.addItem(self.scatter)
        
        # 创建选择区域
        self.region = pg.LinearRegionItem(
            values=(0, 0),
            brush=pg.mkBrush(0, 0, 255, 50),
            hoverBrush=pg.mkBrush(0, 0, 255, 70),
            movable=True
        )
        self.region.hide()
        self.plot.addItem(self.region)
        
        # 创建选中点高亮
        self.highlight = pg.ScatterPlotItem(
            size=12,
            pen=pg.mkPen('b', width=2),
            brush=pg.mkBrush(None)
        )
        self.plot.addItem(self.highlight)
        
        # 数据存储
        self._frame_data: Dict[int, int] = {}
        self._selected_frames: Set[int] = set()
        self._is_selecting = False
        self._selection_start = None
        
        # 连接事件
        self.scatter.sigClicked.connect(self._on_point_clicked)
        self.region.sigRegionChanged.connect(self._on_region_changed)
        self.plot.scene().sigMouseClicked.connect(self._on_plot_clicked)
        self.plot.scene().sigMouseMoved.connect(self._on_mouse_moved)
    
    def update_data(self, frame_data: Dict[int, int]):
        """更新数据
        frame_data: Dict[帧号, 该帧分配的总大小]
        """
        self._frame_data = frame_data
        self._selected_frames.clear()
        self.region.hide()
        self._update_timeline()
    
    def _update_timeline(self):
        """更新时间线图"""
        if not self._frame_data:
            return
            
        # 准备绘图数据
        frames = list(self._frame_data.keys())
        sizes = [size / (1024 * 1024) for size in self._frame_data.values()]  # 转换为MB
        
        # 更新散点图
        self.scatter.setData(
            x=frames,
            y=sizes,
            data=frames  # 存储帧号用于点击事件
        )
        
        # 设置坐标轴范围
        x_min, x_max = min(frames) - 1, max(frames) + 1
        y_max = max(sizes) * 1.1
        self.plot.setXRange(x_min, x_max)
        self.plot.setYRange(0, y_max)
        
        # 更新选择区域范围限制
        self.region.setBounds((x_min, x_max))
        
        # 更新选中点高亮
        self._update_highlight()
    
    def _update_highlight(self):
        """更新选中点的高亮显示"""
        if not self._selected_frames:
            self.highlight.setData([], [])
            return
            
        x = list(self._selected_frames)
        y = [self._frame_data[frame] / (1024 * 1024) for frame in x]
        self.highlight.setData(x=x, y=y)
    
    def _on_point_clicked(self, plot, points):
        """处理点击事件"""
        if not points:
            return
            
        modifiers = QApplication.keyboardModifiers()
        point = points[0]
        frame = point.data()
        
        if modifiers == Qt.ControlModifier:
            # Ctrl+点击：切换选中状态
            if frame in self._selected_frames:
                self._selected_frames.remove(frame)
            else:
                self._selected_frames.add(frame)
        else:
            # 普通点击：单选
            self._selected_frames = {frame}
            self.region.hide()
        
        self._update_highlight()
        self.frames_selected.emit(list(self._selected_frames))
    
    def _on_plot_clicked(self, event):
        """处理绘图区域点击事件"""
        if event.button() == Qt.LeftButton:
            pos = event.scenePos()
            if self.plot.sceneBoundingRect().contains(pos):
                mouse_point = self.plot.vb.mapSceneToView(pos)
                
                if not self._is_selecting:
                    # 开始选择
                    self._is_selecting = True
                    self._selection_start = mouse_point.x()
                    self.region.setRegion((self._selection_start, self._selection_start))
                    self.region.show()
                else:
                    # 结束选择
                    self._is_selecting = False
                    self._selection_start = None
                    self._update_selection_from_region()
    
    def _on_mouse_moved(self, pos):
        """处理鼠标移动事件"""
        if self._is_selecting and self._selection_start is not None:
            if self.plot.sceneBoundingRect().contains(pos):
                mouse_point = self.plot.vb.mapSceneToView(pos)
                self.region.setRegion(sorted([self._selection_start, mouse_point.x()]))
    
    def _on_region_changed(self):
        """处理选择区域变化事件"""
        if not self._is_selecting:  # 只在拖动区域时更新选择
            self._update_selection_from_region()
    
    def _update_selection_from_region(self):
        """根据选择区域更新选中的帧"""
        if not self.region.isVisible():
            return
            
        min_x, max_x = self.region.getRegion()
        self._selected_frames = {
            frame for frame in self._frame_data.keys()
            if min_x <= frame <= max_x
        }
        
        self._update_highlight()
        self.frames_selected.emit(list(self._selected_frames))
    
    def get_frame_info(self, frame: int) -> Tuple[int, List[AllocationRecord]]:
        """获取指定帧的信息"""
        size = self._frame_data.get(frame, 0)
        return size, []  # 由于现在只存储大小，不再返回具体分配记录
    
    def get_selected_frames(self) -> List[int]:
        """获取当前选中的帧列表"""
        return list(self._selected_frames) 