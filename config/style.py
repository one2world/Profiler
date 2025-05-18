"""
全局样式配置
"""

# 主题色
COLORS = {
    "primary": "#2962ff",      # 更鲜艳的蓝色
    "primary_light": "#768fff",
    "primary_dark": "#0039cb",
    "secondary": "#455a64",    # 更深的灰色
    "success": "#00c853",
    "info": "#00b8d4",
    "warning": "#ffd600",
    "danger": "#dd2c00",
    "light": "#f5f5f5",
    "light_hover": "#e0e0e0",
    "dark": "#263238",         # 更深的背景色
    "dark_light": "#4f5b62",
    "white": "#ffffff",
    "transparent": "transparent",
    "border": "#e0e0e0"
}

# 字体
FONTS = {
    "default": "Microsoft YaHei",  # 微软雅黑
    "monospace": "Consolas",
    "size": {
        "tiny": "11px",
        "small": "12px",
        "normal": "14px",
        "large": "16px",
        "xlarge": "18px",
        "title": "20px"
    }
}

# 动画时间
ANIMATIONS = {
    "fast": "150ms",
    "normal": "250ms",
    "slow": "350ms"
}

# 样式表
STYLESHEETS = {
    # 主窗口样式
    "main_window": """
        QMainWindow {
            background-color: %(light)s;
        }
    """ % COLORS,
    
    # 工具栏样式
    "tool_bar": """
        QToolBar {
            background-color: %(dark)s;
            border: none;
            padding: 5px;
            spacing: 5px;
        }
        
        QToolBar QToolButton {
            background-color: transparent;
            border: none;
            border-radius: 4px;
            padding: 5px;
            color: %(white)s;
        }
        
        QToolBar QToolButton:hover {
            background-color: %(dark_light)s;
        }
        
        QToolBar QToolButton:pressed {
            background-color: %(primary)s;
        }
    """ % COLORS,
    
    # 侧边栏分类列表样式
    "category_list": """
        QListWidget {
            background-color: %(dark)s;
            border: none;
            outline: none;
            padding: 5px;
        }
        
        QListWidget::item {
            color: %(white)s;
            background-color: transparent;
            padding: 12px 15px;
            margin: 2px 5px;
            border-radius: 6px;
            font-family: "%(default)s";
            font-size: %(normal)s;
            border: 1px solid transparent;
        }
        
        QListWidget::item:selected {
            background-color: %(primary)s;
            color: %(white)s;
            border: 1px solid %(primary_light)s;
        }
        
        QListWidget::item:hover:!selected {
            background-color: %(dark_light)s;
            border: 1px solid %(dark_light)s;
        }
    """ % (COLORS | {"default": FONTS["default"], "normal": FONTS["size"]["normal"]}),
    
    # 分类标签样式
    "category_label": """
        QLabel {
            color: %(dark)s;
            font-family: "%(default)s";
            font-size: %(large)s;
            font-weight: bold;
            padding: 15px;
            background-color: %(white)s;
            border-bottom: 2px solid %(primary)s;
            border-radius: 8px 8px 0 0;
        }
    """ % (COLORS | {"default": FONTS["default"], "large": FONTS["size"]["large"]}),
    
    # 工具按钮样式
    "tool_button": """
        QPushButton {
            text-align: left;
            padding: 20px;
            border: 1px solid %(border)s;
            border-radius: 10px;
            background-color: %(white)s;
            color: %(dark)s;
            font-family: "%(default)s";
            font-size: %(normal)s;
            min-height: 80px;
        }
        
        QPushButton:hover {
            background-color: %(light)s;
            border-color: %(primary)s;
        }
        
        QPushButton:pressed {
            background-color: %(primary_light)s;
            color: %(white)s;
            border-color: %(primary)s;
        }
    """ % (COLORS | {"default": FONTS["default"], "normal": FONTS["size"]["normal"]}),
    
    # 状态栏样式
    "status_bar": """
        QStatusBar {
            background-color: %(dark)s;
            color: %(white)s;
            font-family: "%(default)s";
            font-size: %(small)s;
            padding: 5px;
        }
        
        QStatusBar::item {
            border: none;
        }
    """ % (COLORS | {"default": FONTS["default"], "small": FONTS["size"]["small"]})
}

# 图标映射（使用更醒目的Emoji）
CATEGORY_ICONS = {
    "performance": "⚡",  # 性能分析
    "testing": "🧪",     # 测试工具
    "dev_tools": "🛠️"    # 开发辅助
}

# 工具图标
TOOL_ICONS = {
    "memory_analyzer": "📊",
    "cpu_analyzer": "💻",
    "fps_analyzer": "🎮",
    "unit_test": "✅",
    "coverage": "📈",
    "json_tool": "📝",
    "log_analyzer": "📋",
    "regex_tester": "🔍"
} 