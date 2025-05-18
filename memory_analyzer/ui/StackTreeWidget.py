from PySide6.QtWidgets import (
    QTreeWidget, QTreeWidgetItem
)
from PySide6.QtCore import Qt, Signal
from typing import List, Dict, Optional, Set
from core.stack_parser import StackTrace, StackFrame
from core.allocation_parser import AllocationRecord

class CallTreeNode:
    """函数调用树节点"""
    def __init__(self, frame: Optional[StackFrame] = None):
        self.frame = frame
        self.children: Dict[str, 'CallTreeNode'] = {}
        self.total_size = 0
        self.allocation_count = 0
        self.stack_hashes: Set[str] = set()
    
    def get_key(self) -> str:
        """获取节点键值"""
        if not self.frame:
            return "root"
        return f"{self.frame.module}::{self.frame.function}"
    
    def add_stack(self, frames: List[StackFrame], hash_id: str, size: int):
        """添加一个调用堆栈"""
        self.total_size += size
        self.allocation_count += 1
        self.stack_hashes.add(hash_id)
        
        if frames:
            frame = frames[0]
            key = f"{frame.module}::{frame.function}"
            if key not in self.children:
                self.children[key] = CallTreeNode(frame)
            self.children[key].add_stack(frames[1:], hash_id, size)

class StackTreeWidget(QTreeWidget):
    """堆栈树视图"""
    
    stack_selected = Signal(str)  # 发送选中的堆栈hash
    
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setHeaderLabels(["函数调用", "总大小", "分配次数"])
        self.setColumnWidth(0, 400)
        self.setColumnWidth(1, 100)
        self.setColumnWidth(2, 80)
        self.itemClicked.connect(self._on_item_clicked)
    
    def update_data(self, traces: Dict[str, StackTrace], allocations: Dict[str, List[AllocationRecord]]):
        """更新数据"""
        self.clear()
        
        # 构建调用树
        root = CallTreeNode()
        for hash_id, trace in traces.items():
            allocs = allocations.get(hash_id, [])
            total_size = sum(a.size for a in allocs)
            if total_size > 0:  # 只添加有分配的堆栈
                root.add_stack(trace.frames, hash_id, total_size)
        
        # 创建树形视图
        self._build_tree_items(self, root)
        
        # 展开第一层
        self.expandToDepth(0)
    
    def _build_tree_items(self, parent, node: CallTreeNode):
        """递归构建树形项目"""
        if node.frame:  # 跳过根节点
            item = QTreeWidgetItem(parent)
            item.setText(0, self._format_frame(node.frame))
            item.setText(1, self._format_size(node.total_size))
            item.setText(2, str(node.allocation_count))
            
            # 存储关联的堆栈hash列表
            item.setData(0, Qt.UserRole, list(node.stack_hashes))
            
            # 设置提示信息
            tooltip = f"模块: {node.frame.module}\n"
            tooltip += f"函数: {node.frame.function}\n"
            if node.frame.source_info:
                tooltip += f"源码: {node.frame.source_info}\n"
            tooltip += f"总大小: {self._format_size(node.total_size)}\n"
            tooltip += f"分配次数: {node.allocation_count}"
            item.setToolTip(0, tooltip)
            
            parent = item
        
        # 按内存大小排序子节点
        sorted_children = sorted(
            node.children.values(),
            key=lambda x: x.total_size,
            reverse=True
        )
        
        # 递归构建子节点
        for child in sorted_children:
            self._build_tree_items(parent, child)
    
    def _format_size(self, size: int) -> str:
        """格式化大小显示"""
        if size < 1024:
            return f"{size} B"
        elif size < 1024 * 1024:
            return f"{size/1024:.1f} KB"
        else:
            return f"{size/1024/1024:.1f} MB"
    
    def _format_frame(self, frame: StackFrame) -> str:
        """格式化堆栈帧显示"""
        if frame.source_info:
            return f"{frame.function} [{frame.source_info}]"
        return f"{frame.function}"
    
    def _on_item_clicked(self, item: QTreeWidgetItem, column: int):
        """处理项目点击事件"""
        hash_list = item.data(0, Qt.UserRole)
        if hash_list:
            # 发送第一个关联的堆栈hash（通常用于展示完整堆栈）
            self.stack_selected.emit(hash_list[0])