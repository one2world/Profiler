from PySide6.QtWidgets import (
    QWidget, QGraphicsView, QGraphicsScene, QGraphicsRectItem,
    QVBoxLayout, QGraphicsSimpleTextItem, QGraphicsItem, QMenu,
    QLabel, QGraphicsDropShadowEffect, QLineEdit, QHBoxLayout,
    QPushButton, QToolTip, QTextEdit
)
from PySide6.QtGui import (
    QColor, QBrush, QPen, QPainter, QFont, QCursor,
    QLinearGradient, QGradient
)
from PySide6.QtCore import (
    QRectF, Qt, Signal, QPointF, QTimer,
    QPropertyAnimation, QEasingCurve, QPoint
)
from typing import Dict, List, Optional
from core.stack_parser import StackTrace
from core.allocation_parser import AllocationRecord
import time


class FlameGraphNode:
    """携带布局信息的火焰图节点"""
    __slots__ = ['name', 'value', 'children', 'parent', 'x', 'y', 'width', 'height', 'color',
                 'hash_id', 'depth', 'size', 'highlighted', 'animation', 'hash_ids']

    """火焰图节点"""
    def __init__(self, name: str, value: float, hash_id: str = None):
        
        self.hash_id = hash_id
        
        self.name = name
        self.value = value  # 总大小
        self.children: List[FlameGraphNode] = []
        
        # 布局属性
        self.parent: Optional[FlameGraphNode] = None
        self.depth = 0
        self.x = 0.0
        self.y = 0.0
        self.width = 0.0
        self.height = 20.0
        self.size = value
        self.highlighted = False
        self.animation = None
        
        # 使用渐变色
        self._generate_color()

    def _generate_color(self):
        """生成渐变色"""
        hue = hash(self.name) % 360
        self.color = QColor.fromHsv(hue, 200, 230)


class FlameGraphView(QGraphicsView):
    """自定义视图类，处理缩放和平移"""
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setRenderHints(QPainter.Antialiasing | QPainter.TextAntialiasing)
        self.setViewportUpdateMode(QGraphicsView.FullViewportUpdate)
        self.setHorizontalScrollBarPolicy(Qt.ScrollBarAlwaysOn)
        self.setVerticalScrollBarPolicy(Qt.ScrollBarAlwaysOn)
        self.setTransformationAnchor(QGraphicsView.AnchorUnderMouse)
        self.setResizeAnchor(QGraphicsView.AnchorUnderMouse)
        self.setDragMode(QGraphicsView.ScrollHandDrag)
        
        # 缓存场景
        self.setCacheMode(QGraphicsView.CacheBackground)
        self.setOptimizationFlag(QGraphicsView.DontAdjustForAntialiasing, True)
        
        self.scale_factor = 1.15
        self._pan_start_pos = None
        self._is_panning = False
        
    def mousePressEvent(self, event):
        """处理鼠标按下事件"""
        if event.button() == Qt.MiddleButton:
            self._pan_start_pos = event.pos()
            self._is_panning = True
            self.setCursor(Qt.ClosedHandCursor)
            event.accept()
        else:
            super().mousePressEvent(event)
    
    def mouseReleaseEvent(self, event):
        """处理鼠标释放事件"""
        if event.button() == Qt.MiddleButton:
            self._is_panning = False
            self._pan_start_pos = None
            self.setCursor(Qt.ArrowCursor)
            event.accept()
        else:
            super().mouseReleaseEvent(event)
    
    def mouseMoveEvent(self, event):
        """处理鼠标移动事件"""
        if self._is_panning and self._pan_start_pos is not None:
            delta = event.pos() - self._pan_start_pos
            self._pan_start_pos = event.pos()
            
            # 计算滚动条的新位置
            h_bar = self.horizontalScrollBar()
            v_bar = self.verticalScrollBar()
            
            h_bar.setValue(h_bar.value() - delta.x())
            v_bar.setValue(v_bar.value() - delta.y())
            
            event.accept()
        else:
            super().mouseMoveEvent(event)
    
    def wheelEvent(self, event):
        """处理滚轮事件"""
        if event.modifiers() == Qt.ControlModifier:
            factor = self.scale_factor if event.angleDelta().y() > 0 else 1 / self.scale_factor
            self.scale(factor, factor)
            event.accept()
        else:
            super().wheelEvent(event)

    def _calculate_layout(self, root: FlameGraphNode):
        """计算节点布局"""
        # 计算总宽度和场景尺寸
        total_width = max(800, self.view.viewport().width() - 20)
        max_depth = self._get_max_depth(root)
        
        def layout_node(node: FlameGraphNode, x: float, depth: int, parent_width: float):
            if self.reverse_stack:
                # 上对齐，从0开始向下
                node.depth = depth
            else:
                # 下对齐，从底部向上
                node.depth = max_depth - depth
            
            node.x = x
            node.y = node.depth * (self.rect_height + self.v_spacing)
            
            # 计算节点宽度（基于父节点宽度的比例）
            if node.parent:
                ratio = node.value / node.parent.value
                node.width = parent_width * ratio
            else:
                node.width = total_width
            
            # 对子节点进行布局
            current_x = x
            for child in sorted(node.children, key=lambda n: -n.value):
                layout_node(child, current_x, depth + 1, node.width)
                current_x += child.width
        
        # 从根节点开始布局
        layout_node(root, 0, 0, total_width)
        
        # 计算所有节点的实际位置范围
        min_y = float('inf')
        max_y = float('-inf')
        
        def update_bounds(node):
            nonlocal min_y, max_y
            if node.parent:  # 跳过根节点
                min_y = min(min_y, node.y)
                max_y = max(max_y, node.y + self.rect_height)
            for child in node.children:
                update_bounds(child)
        
        update_bounds(root)
        
        # 确保至少有一个节点被处理
        if min_y == float('inf'):
            min_y = 0
            max_y = self.rect_height
        
        # 计算场景尺寸
        scene_height = max_y - min_y + self.rect_height  # 添加一个矩形高度的边距
        
        # 调整所有节点的Y坐标
        if self.reverse_stack:
            # 上对齐时，将所有节点向下移动一个矩形高度
            y_offset = self.rect_height
            def adjust_y_up(node):
                if node.parent:
                    node.y += y_offset
                for child in node.children:
                    adjust_y_up(child)
            adjust_y_up(root)
            
            # 设置场景矩形，从0开始
            self.scene.setSceneRect(0, 0, total_width, scene_height + y_offset)
        else:
            # 下对齐时，将所有节点向上移动，使最后一行位于0位置
            y_offset = max_y
            def adjust_y_down(node):
                if node.parent:
                    node.y -= y_offset
                for child in node.children:
                    adjust_y_down(child)
            adjust_y_down(root)
            
            # 设置场景矩形，包含所有负值区域
            self.scene.setSceneRect(0, -scene_height, total_width, scene_height)
        
        # 重置变换以确保正确的对齐
        self.view.resetTransform()
        
        # 调整视图位置
        if self.reverse_stack:
            # 上对齐时滚动到顶部
            self.view.verticalScrollBar().setValue(self.view.verticalScrollBar().minimum())
        else:
            # 下对齐时滚动到底部
            self.view.verticalScrollBar().setValue(self.view.verticalScrollBar().maximum())


class FlameGraphWidget(QWidget):
    """火焰图视图"""
    
    stack_selected = Signal(str)  # 发送选中的堆栈hash
    hovered = Signal(str, float, float)  # 信号: 函数名, 自身(耗时,内存), 总(耗时,内存)    
    
    def __init__(self, parent=None):
        super().__init__(parent)
        self._setup_ui()
        self._setup_colors()
        self._setup_connections()
        
        self.hovered_node = None
        self.selected_node = None
        self.search_results = []
        self.last_update_time = 0
        self.update_interval = 1.0 / 30  # 30 FPS
        
        # 性能优化参数
        self.rect_height = 24
        self.v_spacing = 4
        self.min_width = 1
        self.batch_size = 100  # 批量绘制数量
        
        # 数据存储
        self._traces = {}
        self._allocations = {}
        self._rect_items = {}
        self._node_cache = {}
        self._root = None
        
        # 火焰图配置
        self.reverse_stack = False  # 是否反转堆栈顺序
        
        # 创建更新定时器
        self._update_timer = QTimer(self)
        self._update_timer.timeout.connect(self._perform_delayed_update)
        self._update_timer.setSingleShot(True)
    
    def _setup_colors(self):
        """设置颜色方案"""
        # 基础颜色
        self.colors = {
            'background': QColor(40, 44, 52),  # 深色背景
            'text': QColor(255, 255, 255),     # 白色文本
            'highlight': QColor(97, 175, 239),  # 高亮蓝
            'selection': QColor(73, 138, 244),  # 选中蓝
            'warning': QColor(229, 192, 123),   # 警告黄
            'error': QColor(224, 108, 117),     # 错误红
            'success': QColor(152, 195, 121),   # 成功绿
        }
        
        # 渐变色配置
        self.gradients = {
            'default': {
                'start': QColor(73, 138, 244, 180),  # 半透明蓝色起点
                'end': QColor(73, 138, 244, 255)     # 不透明蓝色终点
            },
            'highlight': {
                'start': QColor(97, 175, 239, 180),
                'end': QColor(97, 175, 239, 255)
            },
            'warning': {
                'start': QColor(229, 192, 123, 180),
                'end': QColor(229, 192, 123, 255)
            }
        }
        
        # 设置场景背景色
        if hasattr(self, 'scene'):
            self.scene.setBackgroundBrush(QBrush(self.colors['background']))
        
        # 设置状态标签样式
        if hasattr(self, 'status_label'):
            self.status_label.setStyleSheet(f"""
                QTextEdit {{
                    background-color: {self.colors['background'].name()};
                    color: {self.colors['text'].name()};
                    border-radius: 4px;
                    border: 1px solid {self.colors['highlight'].name()};
                    selection-background-color: {self.colors['selection'].name()};
                    selection-color: {self.colors['text'].name()};
                    font-size: 12px;
                }}
            """)
        
        # 设置搜索框样式
        if hasattr(self, 'search_input'):
            self.search_input.setStyleSheet(f"""
                QLineEdit {{
                    background-color: {self.colors['background'].name()};
                    color: {self.colors['text'].name()};
                    border: 1px solid {self.colors['highlight'].name()};
                    border-radius: 4px;
                    padding: 4px;
                }}
                QLineEdit:focus {{
                    border: 1px solid {self.colors['selection'].name()};
                }}
            """)
        
        # 设置按钮样式
        button_style = f"""
            QPushButton {{
                background-color: {self.colors['background'].name()};
                color: {self.colors['text'].name()};
                border: 1px solid {self.colors['highlight'].name()};
                border-radius: 4px;
                padding: 4px 8px;
                min-width: 80px;
            }}
            QPushButton:hover {{
                background-color: {self.colors['highlight'].name()};
            }}
            QPushButton:pressed {{
                background-color: {self.colors['selection'].name()};
            }}
        """
        
        if hasattr(self, 'search_prev_btn'):
            self.search_prev_btn.setStyleSheet(button_style)
        if hasattr(self, 'search_next_btn'):
            self.search_next_btn.setStyleSheet(button_style)
        if hasattr(self, 'search_clear_btn'):
            self.search_clear_btn.setStyleSheet(button_style)
    
    def _setup_ui(self):
        """设置UI布局"""
        main_layout = QVBoxLayout(self)
        main_layout.setContentsMargins(0, 0, 0, 0)
        
        # 添加工具栏
        toolbar_layout = QHBoxLayout()
        
        # 添加堆栈顺序切换按钮
        self.order_btn = QPushButton("堆栈顺序")
        self.order_btn.setCheckable(True)
        self.order_btn.setToolTip("切换堆栈顺序：正序（下对齐）/反序（上对齐）")
        toolbar_layout.addWidget(self.order_btn)
        
        toolbar_layout.addStretch()
        
        # 添加搜索栏
        search_layout = QHBoxLayout()
        self.search_input = QLineEdit()
        self.search_input.setPlaceholderText("搜索函数名...")
        self.search_prev_btn = QPushButton("上一个")
        self.search_next_btn = QPushButton("下一个")
        self.search_clear_btn = QPushButton("清除")
        
        search_layout.addWidget(self.search_input)
        search_layout.addWidget(self.search_prev_btn)
        search_layout.addWidget(self.search_next_btn)
        search_layout.addWidget(self.search_clear_btn)
        
        toolbar_layout.addLayout(search_layout)
        
        main_layout.addLayout(toolbar_layout)
        
        # 创建视图和场景
        self.view = FlameGraphView()
        self.scene = QGraphicsScene(self)
        self.view.setScene(self.scene)
        
        main_layout.addWidget(self.view)
        
        # 状态标签（使用QTextEdit替代QLabel以支持文本选择）
        self.status_label = QTextEdit(self)
        self.status_label.setReadOnly(True)
        self.status_label.setVerticalScrollBarPolicy(Qt.ScrollBarAlwaysOff)
        self.status_label.setHorizontalScrollBarPolicy(Qt.ScrollBarAlwaysOff)
        self.status_label.setStyleSheet("""
            QTextEdit {
                background-color: rgba(0, 0, 0, 150);
                color: white;
                border-radius: 4px;
                border: 1px solid rgba(255, 255, 255, 50);
                font-size: 12px;
                padding: 8px;
                selection-background-color: rgba(255, 255, 255, 70);
                selection-color: white;
            }
        """)
        self.status_label.setFixedSize(250, 100)
        self.status_label.hide()
    
    def _setup_connections(self):
        """设置信号连接"""
        self.search_input.textChanged.connect(self._handle_search)
        self.search_prev_btn.clicked.connect(self._goto_prev_result)
        self.search_next_btn.clicked.connect(self._goto_next_result)
        self.search_clear_btn.clicked.connect(self._clear_search)
        self.order_btn.clicked.connect(self._toggle_stack_order)
        self.view.setContextMenuPolicy(Qt.CustomContextMenu)
        self.view.customContextMenuRequested.connect(self._show_context_menu)
    
    def _handle_search(self, text):
        """处理搜索"""
        if not text:
            self._clear_search()
            return
            
        self.search_results = []
        current_time = time.time()
        
        # 限制搜索频率
        if current_time - self.last_update_time < self.update_interval:
            self._update_timer.start(int(self.update_interval * 1000))
            return
            
        self._perform_search(text)
    
    def _perform_search(self, text):
        """执行搜索"""
        text = text.lower()
        for hash_id, rect_item in self._rect_items.items():
            if text in rect_item.data(0).lower():  # data(0)存储函数名
                self.search_results.append(rect_item)
                rect_item.setBrush(QBrush(QColor(255, 255, 0, 100)))
            else:
                rect_item.setBrush(QBrush(self._get_color(rect_item.data(0))))
        
        self.last_update_time = time.time()
    
    def _goto_prev_result(self):
        """跳转到上一个搜索结果"""
        if not self.search_results:
            return
            
        current_idx = self._get_current_result_index()
        prev_idx = (current_idx - 1) % len(self.search_results)
        self._highlight_result(prev_idx)
    
    def _goto_next_result(self):
        """跳转到下一个搜索结果"""
        if not self.search_results:
            return
            
        current_idx = self._get_current_result_index()
        next_idx = (current_idx + 1) % len(self.search_results)
        self._highlight_result(next_idx)
    
    def _get_current_result_index(self):
        """获取当前选中的搜索结果索引"""
        if not self.search_results or not self.selected_node:
            return -1
        
        for i, item in enumerate(self.search_results):
            if item == self.selected_node:
                return i
        return -1
    
    def _highlight_result(self, index):
        """高亮显示搜索结果"""
        if 0 <= index < len(self.search_results):
            item = self.search_results[index]
            self.view.centerOn(item)
            self._on_rect_clicked(item.data(1))  # data(1)存储hash_id
    
    def _clear_search(self):
        """清除搜索结果"""
        for rect_item in self._rect_items.values():
            rect_item.setBrush(QBrush(self._get_color(rect_item.data(0))))
        self.search_results.clear()
        self.search_input.clear()
    
    def _on_rect_clicked(self, node: FlameGraphNode):
        """处理矩形点击事件"""
        # 更新选中状态
        old_selected = self.selected_node
        self.selected_node = node
        
        # 发送所有相关的hash_id
        for hash_id in node.hash_ids:
            self.stack_selected.emit(hash_id)
        
        # 更新受影响的节点的显示
        for rect_item in self._rect_items.values():
            stored_node = rect_item.data(2)
            if stored_node in (old_selected, node):
                # 只更新边框颜色，不重新创建节点
                if stored_node == node:
                    rect_item.setPen(QPen(self.colors['highlight'], 2))
                else:
                    rect_item.setPen(QPen(Qt.black, 0.5))
        
        # 显示状态信息
        total_size = node.value
        total_allocs = sum(len(self._allocations[h]) for h in node.hash_ids)
        self._show_status_info(node.name, total_size, total_allocs)

    def _create_rect_item(self, node: FlameGraphNode) -> QGraphicsRectItem:
        """创建矩形图形项"""
        rect = QRectF(node.x, node.y, max(self.min_width, node.width), self.rect_height)
        rect_item = QGraphicsRectItem(rect)
        
        # 创建渐变色
        gradient = QLinearGradient(rect.topLeft(), rect.bottomLeft())
        
        # 设置基础颜色
        base_color = node.color
        
        # 设置渐变
        gradient.setColorAt(0, base_color.lighter(120))
        gradient.setColorAt(1, base_color)
        gradient.setSpread(QGradient.PadSpread)
        rect_item.setBrush(QBrush(gradient))
        
        # 设置边框
        if node == self.selected_node:
            rect_item.setPen(QPen(self.colors['highlight'], 2))
        else:
            rect_item.setPen(QPen(Qt.black, 0.5))
        
        # 存储数据
        rect_item.setData(0, node.name)  # 函数名
        rect_item.setData(1, node.hash_ids)  # 存储所有相关的hash_ids
        rect_item.setData(2, node)  # 存储节点引用
        
        # 设置工具提示
        tooltip = [f"函数: {node.name}"]
        
        # 计算总大小和分配次数
        total_size = node.value
        total_allocs = sum(len(self._allocations[h]) for h in node.hash_ids)
        
        tooltip.extend([
            f"大小: {total_size/1024/1024:.1f} MB",
            f"分配次数: {total_allocs}"
        ])
        
        if node.parent and node.parent is not self._root:
            tooltip.append(f"占父节点比例: {(node.value/node.parent.value)*100:.1f}%")
        
        if len(node.hash_ids) > 1:
            tooltip.append(f"合并堆栈数: {len(node.hash_ids)}")
        
        rect_item.setToolTip("\n".join(tooltip))
        
        # 添加阴影效果
        if node.width > 50:  # 只为较大的矩形添加阴影
            shadow = QGraphicsDropShadowEffect()
            shadow.setBlurRadius(4)
            shadow.setOffset(2, 2)
            shadow.setColor(QColor(0, 0, 0, 100))
            rect_item.setGraphicsEffect(shadow)
        
        # 设置交互
        rect_item.setAcceptHoverEvents(True)
        rect_item.mousePressEvent = lambda e: self._on_rect_clicked(node)
        
        return rect_item

    def _create_text_item(self, node: FlameGraphNode, rect: QRectF) -> Optional[QGraphicsSimpleTextItem]:
        """创建文本图形项"""
        if rect.width() <= 30:
            return None
            
        text_item = QGraphicsSimpleTextItem(node.name)
        text_item.setBrush(Qt.white)
        
        # 设置字体
        font = QFont("Arial", 8)
        text_item.setFont(font)
        
        # 调整文本位置
        text_bounds = text_item.boundingRect()
        text_x = rect.left() + 4
        text_y = rect.top() + (rect.height() - text_bounds.height()) / 2
        text_item.setPos(text_x, text_y)
        
        # 如果文本超出矩形宽度，添加省略号
        if text_bounds.width() > rect.width() - 8:
            text = node.name
            while text and text_bounds.width() > rect.width() - 20:
                text = text[:-1]
                text_item.setText(text + "...")
                text_bounds = text_item.boundingRect()
        
        return text_item
    
    def _perform_delayed_update(self):
        """执行延迟更新"""
        if self.search_input.text():
            self._perform_search(self.search_input.text())
    
    def update_data(self, traces: Dict[str, StackTrace], allocations: Dict[str, List[AllocationRecord]]):
        """更新数据"""
        self._traces = traces
        self._allocations = allocations
        self._update_flame_graph()
    
    def _update_flame_graph(self):
        """更新火焰图"""
        # 清理场景和缓存
        self.scene.clear()
        self._rect_items.clear()
        self._node_cache.clear()
        
        if not self._traces or not self._allocations:
            return
        
        # 构建节点树
        self._root = self._build_node_tree()  # 保存根节点引用
        
        # 计算布局
        self._calculate_layout(self._root)
        
        # 批量绘制节点
        self._draw_nodes_batch(self._root)
        
        # 调整视图
        self.view.fitInView(self.scene.sceneRect(), Qt.KeepAspectRatio)
    
    def _build_node_tree(self) -> FlameGraphNode:
        """构建节点树"""
        # 创建根节点
        root = FlameGraphNode("root", 0)
        root.value = 0
        root.hash_ids = set()
        
        # 计算每个堆栈的大小
        stack_sizes = {
            hash_id: sum(a.size for a in allocs)
            for hash_id, allocs in self._allocations.items()
        }
        
        # 按大小排序
        sorted_stacks = sorted(
            stack_sizes.items(),
            key=lambda x: x[1],
            reverse=True
        )
        
        # 构建树
        for hash_id, size in sorted_stacks:
            trace = self._traces[hash_id]
            
            # 从根节点开始
            current = root
            root.value += size
            root.hash_ids.add(hash_id)
            
            # 根据顺序选择帧的遍历方式
            frames = trace.frames if not self.reverse_stack else reversed(trace.frames)
            
            for frame in frames:
                # 查找或创建子节点
                child = None
                for existing in current.children:
                    if existing.name == frame.function:
                        child = existing
                        break
                
                if child is None:
                    child = FlameGraphNode(frame.function, size)
                    child.parent = current
                    child.hash_ids = {hash_id}
                    current.children.append(child)
                else:
                    child.value += size
                    child.hash_ids.add(hash_id)
                
                current = child
        
        return root

    def _get_max_depth(self, node: FlameGraphNode) -> int:
        """获取最大深度"""
        if not node.children:
            return node.depth
        return max(self._get_max_depth(child) for child in node.children)
    
    def _draw_nodes_batch(self, root: FlameGraphNode):
        """批量绘制节点"""
        nodes_to_draw = []
        self._collect_nodes_to_draw(root, nodes_to_draw)
        
        # 清理场景
        self.scene.clear()
        self._rect_items.clear()
        
        for i in range(0, len(nodes_to_draw), self.batch_size):
            batch = nodes_to_draw[i:i + self.batch_size]
            for node in batch:
                if node.parent:  # 跳过根节点
                    # 创建并添加矩形项
                    rect_item = self._create_rect_item(node)
                    self.scene.addItem(rect_item)
                    self._rect_items[id(node)] = rect_item
                    
                    # 创建并添加文本项
                    text_item = self._create_text_item(node, rect_item.rect())
                    if text_item:
                        self.scene.addItem(text_item)
    
    def _collect_nodes_to_draw(self, node: FlameGraphNode, nodes: List[FlameGraphNode]):
        """收集需要绘制的节点"""
        nodes.append(node)
        for child in node.children:
            self._collect_nodes_to_draw(child, nodes)
    
    def _show_context_menu(self, pos):
        """显示上下文菜单"""
        menu = QMenu(self)
        zoom_in = menu.addAction("放大")
        zoom_out = menu.addAction("缩小")
        fit_view = menu.addAction("适应视图")
        menu.addSeparator()
        reset_view = menu.addAction("重置视图")
        
        action = menu.exec_(self.view.mapToGlobal(pos))
        if action == zoom_in:
            self.view.scale(self.scale_factor, self.scale_factor)
        elif action == zoom_out:
            self.view.scale(1/self.scale_factor, 1/self.scale_factor)
        elif action == fit_view:
            self.view.fitInView(self.scene.sceneRect(), Qt.KeepAspectRatio)
        elif action == reset_view:
            self.view.resetTransform()
    
    def _show_status_info(self, name: str, size: int, alloc_count: int):
        """显示状态信息"""
        text = (
            f"函数: {name}\n"
            f"大小: {size/1024/1024:.1f} MB\n"
            f"分配次数: {alloc_count}"
        )
        self.status_label.setText(text)
        
        # 获取视图的可见区域
        viewport_rect = self.view.viewport().rect()
        viewport_pos = self.view.mapToGlobal(viewport_rect.topLeft())
        
        # 计算提示框位置
        margin = 10
        x = viewport_pos.x() + viewport_rect.width() - self.status_label.width() - margin
        
        if self.reverse_stack:
            # 反序时显示在右下角
            y = viewport_pos.y() + viewport_rect.height() - self.status_label.height() - margin
        else:
            # 正序时显示在右上角
            y = viewport_pos.y() + margin
        
        # 将全局坐标转换为窗口坐标
        pos = self.mapFromGlobal(QPoint(x, y))
        self.status_label.move(pos)
        self.status_label.show()
        self.status_label.raise_()
    
    def resizeEvent(self, event):
        """处理窗口大小变化"""
        super().resizeEvent(event)
        if self._root:
            # 重新计算布局以保持贴合
            self._calculate_layout(self._root)
            
            # 更新状态标签位置
            if self.status_label.isVisible() and self.selected_node:
                self._show_status_info(
                    self.selected_node.name,
                    self.selected_node.value,
                    sum(len(self._allocations[h]) for h in self.selected_node.hash_ids)
                )

    def _get_color(self, name: str) -> QColor:
        """根据函数名生成颜色，使用预定义的渐变色"""
        # 根据函数名特征选择不同的基础色调
        if 'error' in name.lower() or 'exception' in name.lower():
            base_color = self.colors['error']
        elif 'warning' in name.lower():
            base_color = self.colors['warning']
        elif 'success' in name.lower() or 'ok' in name.lower():
            base_color = self.colors['success']
        else:
            # 使用默认的HSV颜色生成
            hue = hash(name) % 360
            saturation = 200
            value = 230
            base_color = QColor.fromHsv(hue, saturation, value)
        
        # 稍微调整基础色调，使其有些变化但保持在同一色系
        return base_color.lighter(100 + (hash(name) % 30))

    def _toggle_stack_order(self):
        """切换堆栈顺序"""
        self.reverse_stack = self.order_btn.isChecked()
        self.order_btn.setText("堆栈顺序(反序)" if self.reverse_stack else "堆栈顺序(正序)")
        
        # 更新火焰图
        if self._root:
            self._update_flame_graph()
            
            # 如果有选中的节点，更新状态信息位置
            if self.selected_node and self.status_label.isVisible():
                total_size = self.selected_node.value
                total_allocs = sum(len(self._allocations[h]) for h in self.selected_node.hash_ids)
                self._show_status_info(self.selected_node.name, total_size, total_allocs)

