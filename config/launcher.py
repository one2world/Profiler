"""
模块启动器，负责进程管理和模块启动
"""

import os
import sys
import time
import signal
import subprocess
from typing import Dict, Optional, List
from dataclasses import dataclass
from pathlib import Path

@dataclass
class ProcessInfo:
    """进程信息"""
    process: subprocess.Popen
    module_id: str
    start_time: float
    entry_script: str

class ModuleLauncher:
    """模块启动器"""
    
    def __init__(self, python_path: str = "python", working_dir: str = ".", 
                 launch_timeout: int = 30, env_vars: Dict[str, str] = None):
        self.python_path = python_path
        self.working_dir = working_dir
        self.launch_timeout = launch_timeout
        self.env_vars = env_vars or {}
        
        # 进程管理
        self._processes: Dict[str, ProcessInfo] = {}
        
        # 确保工作目录存在
        os.makedirs(working_dir, exist_ok=True)
    
    def launch_module(self, module_id: str, entry_script: str, working_dir: str = None) -> bool:
        """启动模块
        
        Args:
            module_id: 模块ID
            entry_script: 入口脚本路径
            
        Returns:
            bool: 是否启动成功
        """
        # 检查模块是否已经在运行
        if module_id in self._processes:
            if self.is_process_running(module_id):
                print(f"模块 {module_id} 已在运行中")
                return False
            else:
                # 清理已退出的进程
                self.cleanup_process(module_id)
        
        try:
            # 准备环境变量
            env = os.environ.copy()
            env.update(self.env_vars)
            
            # 构建启动命令
            script_path = os.path.join(self.working_dir, entry_script)
            cmd = [self.python_path, script_path]
            
            # 启动进程
            process = subprocess.Popen(
                cmd,
                env=env,
                cwd=working_dir or self.working_dir,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
                creationflags=subprocess.CREATE_NEW_PROCESS_GROUP  # Windows特定
            )
            
            # 记录进程信息
            self._processes[module_id] = ProcessInfo(
                process=process,
                module_id=module_id,
                start_time=time.time(),
                entry_script=entry_script
            )
            
            # 等待进程启动
            return self._wait_for_module_start(module_id)
            
        except Exception as e:
            print(f"启动模块 {module_id} 失败: {str(e)}")
            return False
    
    def _wait_for_module_start(self, module_id: str) -> bool:
        """等待模块启动完成"""
        if module_id not in self._processes:
            return False
            
        process_info = self._processes[module_id]
        start_time = time.time()
        
        while time.time() - start_time < self.launch_timeout:
            if process_info.process.poll() is not None:
                # 进程已退出
                stdout, stderr = process_info.process.communicate()
                print(f"模块 {module_id} 启动失败:")
                if stdout: print(f"标准输出: {stdout}")
                if stderr: print(f"错误输出: {stderr}")
                return False
                
            # TODO: 这里可以添加更多的启动检查逻辑
            # 例如检查某个端口、文件或其他标志
            
            time.sleep(0.1)
            return True  # 暂时直接返回成功
            
        # 超时处理
        print(f"模块 {module_id} 启动超时")
        self.terminate_module(module_id)
        return False
    
    def terminate_module(self, module_id: str) -> bool:
        """终止模块"""
        if module_id not in self._processes:
            return False
            
        process_info = self._processes[module_id]
        
        try:
            if sys.platform == 'win32':
                # Windows下使用CTRL_BREAK_EVENT
                process_info.process.send_signal(signal.CTRL_BREAK_EVENT)
            else:
                # Unix下使用SIGTERM
                process_info.process.terminate()
            
            # 等待进程退出
            process_info.process.wait(timeout=5)
            return True
            
        except Exception as e:
            print(f"终止模块 {module_id} 失败: {str(e)}")
            # 强制结束进程
            process_info.process.kill()
            return False
        finally:
            self.cleanup_process(module_id)
    
    def cleanup_process(self, module_id: str):
        """清理进程信息"""
        if module_id in self._processes:
            del self._processes[module_id]
    
    def is_process_running(self, module_id: str) -> bool:
        """检查进程是否在运行"""
        if module_id not in self._processes:
            return False
        return self._processes[module_id].process.poll() is None
    
    def get_running_modules(self) -> List[str]:
        """获取正在运行的模块列表"""
        return [
            module_id for module_id in self._processes.keys()
            if self.is_process_running(module_id)
        ]
    
    def cleanup_all(self):
        """清理所有进程"""
        for module_id in list(self._processes.keys()):
            self.terminate_module(module_id)
    
    def __del__(self):
        """析构时清理所有进程"""
        self.cleanup_all() 