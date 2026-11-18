"""轻量桌面前端；后台子进程执行工具，不在 UI 线程构建或更新。"""
from pathlib import Path
import queue
import subprocess
import sys
import threading
import tkinter as tk
from tkinter import filedialog, ttk
from paths import project_default, output_default


def show():
    window = tk.Tk()
    window.title('DarwinCoreBuilder · 服务端核心构建与更新')
    window.geometry('880x600')
    values = {}
    frame = ttk.Frame(window, padding=18)
    frame.pack(fill='both', expand=True)
    fields = [('unity', 'Unity.exe', ''), ('project', 'Unity 工程', str(project_default() or '')),
              ('output', '更新包输出目录', str(output_default())),
              ('root', '服务器部署目录', ''), ('package', 'ZIP 更新包', '')]
    for row, (name, label, default) in enumerate(fields):
        ttk.Label(frame, text=label).grid(row=row, column=0, sticky='w', pady=5)
        value = values[name] = tk.StringVar(value=default)
        ttk.Entry(frame, textvariable=value).grid(row=row, column=1, sticky='ew', padx=8)
        def browse(n=name, v=value):
            selected = filedialog.askopenfilename() if n in ('unity', 'package') else filedialog.askdirectory()
            if selected:
                v.set(selected)
        ttk.Button(frame, text='选择', command=browse).grid(row=row, column=2)
    target = tk.StringVar(value='windows-headless')
    ttk.Combobox(frame, textvariable=target, values=['windows-headless', 'windows-server', 'linux-server'], state='readonly').grid(row=5, column=1, sticky='w', pady=8)
    ttk.Label(frame, text='更新会停服保存并备份；成功后开服，失败则恢复。部署功能目前支持 Windows。').grid(row=6, column=0, columnspan=3, sticky='w')
    actions = ttk.Frame(frame)
    actions.grid(row=7, column=0, columnspan=3, sticky='w', pady=10)
    output = tk.Text(frame, wrap='word', state='disabled')
    output.grid(row=8, column=0, columnspan=3, sticky='nsew')
    frame.columnconfigure(1, weight=1)
    frame.rowconfigure(8, weight=1)
    events = queue.Queue()
    buttons = []
    running = False

    def execute(command):
        nonlocal running
        if running:
            return
        args = [sys.executable, '-u', str(Path(__file__).with_name('main.py')), command]
        keys = {'build': ('unity', 'project', 'output'), 'verify': ('package',),
                'update': ('root', 'package'), 'start': ('root',), 'stop': ('root',), 'doctor': ('unity', 'project')}[command]
        for name in keys:
            if not values[name].get().strip():
                events.put(f'请先填写：{name}\n')
                return
            if command != 'verify':
                args.append('--' + name)
            args.append(values[name].get())
        if command == 'build':
            args += ['--target', target.get()]
        running = True
        for button in buttons:
            button.configure(state='disabled')
        def worker():
            try:
                with subprocess.Popen(args, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True,
                                      encoding='utf-8', errors='replace', env={**__import__('os').environ, 'PYTHONUTF8': '1'},
                                      creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0)) as process:
                    for line in process.stdout:
                        events.put(line)
                    events.put(f'退出码：{process.wait()}\n')
            except Exception as error:
                events.put(str(error) + '\n')
            finally:
                events.put(None)
        threading.Thread(target=worker, daemon=True).start()

    for label, command in [('构建核心包', 'build'), ('校验更新包', 'verify'), ('安装 / 更新并开服', 'update'), ('启动', 'start'), ('保存停服', 'stop'), ('环境检查', 'doctor')]:
        button = ttk.Button(actions, text=label, command=lambda c=command: execute(c))
        button.pack(side='left', padx=3)
        buttons.append(button)

    def poll():
        nonlocal running
        while not events.empty():
            line = events.get_nowait()
            if line is None:
                running = False
                for button in buttons:
                    button.configure(state='normal')
            else:
                output.configure(state='normal')
                output.insert('end', line)
                output.see('end')
                output.configure(state='disabled')
        window.after(150, poll)
    # 构建/更新中保持窗口，避免用户误以为关闭 UI 就取消了停服事务。
    window.protocol('WM_DELETE_WINDOW', lambda: events.put('任务进行中，请等待完成后关闭。\n') if running else window.destroy())
    poll()
    window.mainloop()
