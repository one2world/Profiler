import sys
import os
from PySide6.QtWidgets import (
    QApplication, QMainWindow, QWidget, QVBoxLayout, QHBoxLayout,
    QListWidget, QStackedWidget, QLabel, QStatusBar,
    QToolBar, QStyle, QListWidgetItem, QPushButton, QGridLayout,
    QMessageBox
)
from PySide6.QtCore import Qt, QSize
from PySide6.QtGui import QAction, QIcon, QFont

from config.module_config import TOOL_CATEGORIES, LAUNCH_CONFIG
from config.launcher import ModuleLauncher
from config.style import (
    STYLESHEETS, CATEGORY_ICONS, TOOL_ICONS,
    FONTS, COLORS, ANIMATIONS
)

class CategoryListItem(QListWidgetItem):
    """自定义分类列表项"""
    def __init__(self, category: dict, parent=None):
        super().__init__(parent)
        self.category = category
        
        # 设置图标和文本
        icon = CATEGORY_ICONS.get(category["id"], "📦")
        self.setText(f"{icon}  {category['name']}")
        
        # 设置提示
        self.setToolTip(category["description"])
        
        # 设置字体
        font = QFont(FONTS["default"])
        font.setPixelSize(int(FONTS["size"]["normal"].replace("px", "")))
        self.setFont(font)

class ToolButton(QPushButton):
    """自定义工具按钮"""
    def __init__(self, module_info: dict, parent=None):
        super().__init__(parent)
        self.module_info = module_info
        
        # 设置图标和文本
        icon = TOOL_ICONS.get(module_info["id"], "🔧")
        self.setText(f"{icon}  {module_info['name']}\n{module_info['description']}")
        
        # 设置样式表
        self.setStyleSheet(STYLESHEETS["tool_button"])

class MainWindow(QMainWindow):
    """程序员开发工具集合平台主窗口"""
    
    def __init__(self):
        super().__init__()
        self.setWindowTitle("程序员开发工具集合平台")
        self.resize(1200, 800)
        
        # 初始化模块启动器
        self.module_launcher = ModuleLauncher(
            python_path=LAUNCH_CONFIG["python_path"],
            working_dir=LAUNCH_CONFIG["working_dir"],
            launch_timeout=LAUNCH_CONFIG["launch_timeout"],
            env_vars=LAUNCH_CONFIG["env_vars"]
        )
        
        # 设置窗口样式
        self.setStyleSheet(STYLESHEETS["main_window"])
        
        self.setup_ui()
        self.load_modules()
    
    def setup_ui(self):
        # 工具栏
        toolbar = QToolBar()
        toolbar.setMovable(False)
        toolbar.setStyleSheet(STYLESHEETS["tool_bar"])
        self.addToolBar(toolbar)
        
        # 添加工具栏按钮
        settings_action = QAction(
            self.style().standardIcon(QStyle.SP_FileDialogDetailedView),
            "设置",
            self
        )
        settings_action.setStatusTip("平台全局设置")
        toolbar.addAction(settings_action)
        
        theme_action = QAction(
            self.style().standardIcon(QStyle.SP_DesktopIcon),
            "主题",
            self
        )
        theme_action.setStatusTip("切换明暗主题")
        toolbar.addAction(theme_action)
        
        help_action = QAction(
            self.style().standardIcon(QStyle.SP_DialogHelpButton),
            "帮助",
            self
        )
        help_action.setStatusTip("查看帮助文档")
        toolbar.addAction(help_action)
        
        # 主界面布局
        main_widget = QWidget()
        main_layout = QVBoxLayout(main_widget)
        main_layout.setSpacing(0)
        main_layout.setContentsMargins(0, 0, 0, 0)
        
        # 分类列表
        self.category_list = QListWidget()
        self.category_list.setFixedWidth(220)
        self.category_list.setStyleSheet(STYLESHEETS["category_list"])
        self.category_list.currentRowChanged.connect(self.switch_category)
        
        # 工具列表区域
        self.tools_stack = QStackedWidget()
        
        # 水平布局
        layout = QHBoxLayout()
        layout.setSpacing(0)
        layout.addWidget(self.category_list)
        layout.addWidget(self.tools_stack)
        main_layout.addLayout(layout)
        
        self.setCentralWidget(main_widget)
        
        # 状态栏
        self.statusBar().setStyleSheet(STYLESHEETS["status_bar"])
        self.statusBar().showMessage("就绪")
    
    def load_modules(self):
        """加载所有模块"""
        for category in TOOL_CATEGORIES:
            # 添加分类
            category_item = CategoryListItem(category)
            self.category_list.addItem(category_item)
            
            # 创建该分类的工具列表
            tools_widget = QWidget()
            tools_layout = QVBoxLayout(tools_widget)
            tools_layout.setContentsMargins(20, 20, 20, 20)
            tools_layout.setSpacing(20)
            
            # 添加分类说明
            category_label = QLabel(f"【{category['name']}】")
            category_label.setStyleSheet(STYLESHEETS["category_label"])
            tools_layout.addWidget(category_label)
            
            # 创建网格布局来放置工具按钮
            tools_grid = QGridLayout()
            tools_grid.setSpacing(15)
            
            # 添加工具按钮
            for i, module in enumerate(category["modules"]):
                button = ToolButton(module)
                button.clicked.connect(self.launch_tool)
                
                # 每行放置2个按钮
                row = i // 2
                col = i % 2
                tools_grid.addWidget(button, row, col)
            
            # 设置列的拉伸因子
            tools_grid.setColumnStretch(0, 1)
            tools_grid.setColumnStretch(1, 1)
            
            # 添加网格布局
            tools_layout.addLayout(tools_grid)
            
            # 添加弹性空间
            tools_layout.addStretch()
            
            self.tools_stack.addWidget(tools_widget)
    
    def switch_category(self, index: int):
        """切换工具分类"""
        self.tools_stack.setCurrentIndex(index)
        category = TOOL_CATEGORIES[index]
        self.statusBar().showMessage(f"当前分类：{category['name']}")
    
    def launch_tool(self):
        """启动工具模块"""
        button = self.sender()
        if isinstance(button, ToolButton):
            module_info = button.module_info
            module_id = module_info["id"]
            entry_script = module_info["entry_script"]
            
            try:
                if self.module_launcher.is_process_running(module_id):
                    QMessageBox.information(
                        self,
                        "提示",
                        f"模块 {module_info['name']} 已在运行中"
                    )
                    return
                
                self.statusBar().showMessage(f"正在启动：{module_info['name']}...")
                working_dir = os.path.dirname(entry_script)
                # entry_script移除working_dir
                entry_script = entry_script.replace(working_dir, "").lstrip("/\\")

                if self.module_launcher.launch_module(module_id, entry_script, working_dir=working_dir):
                    self.statusBar().showMessage(f"已启动：{module_info['name']}")
                else:
                    self.statusBar().showMessage(f"启动失败：{module_info['name']}")
                    QMessageBox.warning(
                        self,
                        "错误",
                        f"启动模块 {module_info['name']} 失败"
                    )
            except Exception as e:
                self.statusBar().showMessage(f"启动失败：{str(e)}")
                QMessageBox.critical(
                    self,
                    "错误",
                    f"启动模块时发生错误：{str(e)}"
                )
    
    def closeEvent(self, event):
        """关闭事件处理"""
        # 检查是否有正在运行的模块
        running_modules = self.module_launcher.get_running_modules()
        if running_modules:
            reply = QMessageBox.question(
                self,
                "确认退出",
                "还有正在运行的工具，确定要退出吗？",
                QMessageBox.Yes | QMessageBox.No,
                QMessageBox.No
            )
            
            if reply == QMessageBox.No:
                event.ignore()
                return
        
        # 清理所有进程
        self.module_launcher.cleanup_all()
        event.accept()

def main():
    app = QApplication(sys.argv)
    
    # 设置应用样式
    app.setStyle("Fusion")
    
    # 设置全局字体
    app.setFont(QFont(FONTS["default"]))
    
    window = MainWindow()
    window.show()
    
    sys.exit(app.exec())

if __name__ == "__main__":
    main() 