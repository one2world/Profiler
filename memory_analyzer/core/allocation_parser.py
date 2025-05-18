from typing import Dict, List, Optional
from dataclasses import dataclass
from collections import defaultdict

@dataclass
class AllocationRecord:
    """内存分配记录"""
    size: int
    hash_id: str
    address: str
    object_type: str
    frame: Optional[int] = None

class AllocationParser:
    """内存分配文件解析器"""
    
    def __init__(self):
        self.records: List[AllocationRecord] = []
        self._frame_data: Dict[int, List[AllocationRecord]] = defaultdict(list)
        self._hash_data: Dict[str, List[AllocationRecord]] = defaultdict(list)
    
    def parse_file(self, file_path: str) -> List[AllocationRecord]:
        """解析分配文件"""
        current_record = {}
        
        with open(file_path, 'r') as f:
            for line in f:
                line = line.strip()
                if not line:
                    if current_record:
                        record = self._create_record(current_record)
                        self._add_record(record)
                        current_record = {}
                else:
                    key, value = line.split(':', 1)
                    current_record[key.strip()] = value.strip()
            
            if current_record:
                record = self._create_record(current_record)
                self._add_record(record)
        
        return self.records
    
    def _create_record(self, data: Dict[str, str]) -> AllocationRecord:
        """从原始数据创建分配记录"""
        return AllocationRecord(
            size=int(data.get('size', 0)),
            hash_id=data.get('hash', ''),
            address=data.get('addr', ''),
            object_type=data.get('object', ''),
            frame=int(data['frame']) if 'frame' in data else None
        )
    
    def _add_record(self, record: AllocationRecord):
        """添加分配记录并更新索引"""
        self.records.append(record)
        if record.frame is not None:
            self._frame_data[record.frame].append(record)
        self._hash_data[record.hash_id].append(record)
    
    def get_frame_allocations(self, frame: int) -> List[AllocationRecord]:
        """获取指定帧的所有分配"""
        return self._frame_data.get(frame, [])
    
    def get_hash_allocations(self, hash_id: str) -> List[AllocationRecord]:
        """获取指定堆栈的所有分配"""
        return self._hash_data.get(hash_id, [])
    
    def get_frame_total_size(self, frame: int) -> int:
        """获取指定帧的总分配大小"""
        return sum(record.size for record in self.get_frame_allocations(frame))
    
    def get_hash_total_size(self, hash_id: str) -> int:
        """获取指定堆栈的总分配大小"""
        return sum(record.size for record in self.get_hash_allocations(hash_id))
    
    def get_frame_range(self) -> tuple[int, int]:
        """获取帧号范围"""
        frames = [record.frame for record in self.records if record.frame is not None]
        return (min(frames), max(frames)) if frames else (0, 0)
    
    def get_size_statistics(self) -> Dict[str, int]:
        """获取大小统计信息"""
        stats = {
            'total_size': sum(record.size for record in self.records),
            'total_count': len(self.records),
            'unique_stacks': len(self._hash_data),
        }
        if self.records:
            stats.update({
                'min_size': min(record.size for record in self.records),
                'max_size': max(record.size for record in self.records),
                'avg_size': stats['total_size'] // stats['total_count']
            })
        return stats 