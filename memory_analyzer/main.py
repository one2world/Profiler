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

from ui.MemoryAnalyzerWindow import MemoryAnalyzerWindow

def main():
    app = QApplication(sys.argv)
    
    # 设置应用样式
    app.setStyle("Fusion")
        
    window = MemoryAnalyzerWindow()
    window.show()
    
    sys.exit(app.exec())

if __name__ == "__main__":
    main() 