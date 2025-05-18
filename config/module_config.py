"""
模块配置文件，定义所有工具模块的配置信息
"""

TOOL_CATEGORIES = [
    {
        "name": "性能分析工具",
        "id": "performance",
        "description": "性能分析相关工具集",
        "modules": [
            {
                "name": "内存分析器",
                "id": "memory_analyzer",
                "description": "内存分配、泄漏分析工具",
                "entry_script": "memory_analyzer/main.py",
                "icon": "memory.png"
            },
            {
                "name": "CPU分析器",
                "id": "cpu_analyzer", 
                "description": "CPU使用率、热点分析工具",
                "entry_script": "performance_analyzer/cpu_analyzer.py",
                "icon": "cpu.png"
            },
            {
                "name": "帧率分析",
                "id": "fps_analyzer",
                "description": "FPS波动分析工具",
                "entry_script": "performance_analyzer/fps_analyzer.py",
                "icon": "fps.png"
            }
        ]
    },
    {
        "name": "测试工具",
        "id": "testing",
        "description": "测试相关工具集",
        "modules": [
            {
                "name": "单元测试管理",
                "id": "unit_test",
                "description": "单元测试用例管理工具",
                "entry_script": "test_tools/unit_test.py",
                "icon": "test.png"
            },
            {
                "name": "覆盖率分析",
                "id": "coverage",
                "description": "代码覆盖率可视化工具",
                "entry_script": "test_tools/coverage.py",
                "icon": "coverage.png"
            }
        ]
    },
    {
        "name": "开发辅助工具",
        "id": "dev_tools",
        "description": "日常开发辅助工具集",
        "modules": [
            {
                "name": "JSON工具",
                "id": "json_tool",
                "description": "JSON格式化、验证、比较工具",
                "entry_script": "dev_tools/json_tool.py",
                "icon": "json.png"
            },
            {
                "name": "日志分析器",
                "id": "log_analyzer",
                "description": "日志搜索、过滤工具",
                "entry_script": "dev_tools/log_analyzer.py",
                "icon": "log.png"
            },
            {
                "name": "正则测试器",
                "id": "regex_tester",
                "description": "正则表达式测试工具",
                "entry_script": "dev_tools/regex_tester.py",
                "icon": "regex.png"
            }
        ]
    }
]

# 模块启动配置
LAUNCH_CONFIG = {
    "python_path": "python",  # Python解释器路径
    "launch_timeout": 30,     # 启动超时时间(秒)
    "working_dir": ".",      # 工作目录
    "env_vars": {            # 环境变量
        "PYTHONPATH": ".",
        "TOOL_DEBUG": "1"
    }
} 