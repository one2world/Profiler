from typing import Dict, List, Optional
from dataclasses import dataclass
import re

@dataclass
class StackFrame:
    """堆栈帧信息"""
    address: str
    module: str
    function: str
    source_info: Optional[str] = None
    
    @staticmethod
    def parse(line: str) -> 'StackFrame':
        """解析堆栈帧信息"""
        # 匹配格式：address (module) [source] function
        pattern = r'(0x[0-9a-fA-F]+)\s+\(([^)]+)\)(?:\s+\[([^]]+)\])?\s*(.+)?'
        match = re.match(pattern, line.strip())
        if match:
            addr, module, source, func = match.groups()
            return StackFrame(addr, module, func or "", source)
        else:
            # 简单格式：只有函数名
            return StackFrame("", "", line.strip(), None)

class StackTrace:
    """完整堆栈信息"""
    def __init__(self, hash_id: str, frames: List[StackFrame]):
        self.hash_id = hash_id
        self.frames = frames
        
    @property
    def top_frame(self) -> Optional[StackFrame]:
        """获取顶层帧"""
        return self.frames[0] if self.frames else None
    
    def get_call_path(self) -> str:
        """获取调用路径"""
        return " -> ".join(f.function for f in self.frames)

class StackParser:
    """堆栈文件解析器"""
    
    def __init__(self):
        self.traces: Dict[str, StackTrace] = {}
    
    def parse_file(self, file_path: str) -> Dict[str, StackTrace]:
        """解析堆栈文件"""
        current_hash = None
        current_frames = []
        
        with open(file_path, 'r') as f:
            for line in f:
                line = line.strip()
                if line.startswith('hash:'):
                    if current_hash and current_frames:
                        self.traces[current_hash] = StackTrace(current_hash, current_frames)
                        current_frames = []
                    current_hash = line.split(':')[1].strip()
                elif line:
                    frame = StackFrame.parse(line)
                    current_frames.append(frame)
            
            if current_hash and current_frames:
                self.traces[current_hash] = StackTrace(current_hash, current_frames)
        
        return self.traces
    
    def get_trace(self, hash_id: str) -> Optional[StackTrace]:
        """获取指定hash的堆栈信息"""
        return self.traces.get(hash_id)
    
    def get_all_traces(self) -> Dict[str, StackTrace]:
        """获取所有堆栈信息"""
        return self.traces 