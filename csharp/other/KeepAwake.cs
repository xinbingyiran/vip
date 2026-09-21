using System;
using System.Runtime.InteropServices;

// ============ 顶级语句（程序入口） ============
const uint ES_CONTINUOUS = 0x80000000;
const uint ES_SYSTEM_REQUIRED = 0x00000001;
const uint ES_DISPLAY_REQUIRED = 0x00000002;

Console.WriteLine("防睡眠工具已启动：已阻止系统睡眠、休眠和显示器关闭。请保持此窗口运行，最小化即可，按任意键退出并恢复系统默认电源行为。");

// 设置一次，ES_CONTINUOUS 持续有效直到进程退出或显式清除
Native.SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);

Console.ReadKey(true); // 阻塞等待按键，随时退出

// 退出前恢复默认行为
Native.SetThreadExecutionState(ES_CONTINUOUS);
Console.WriteLine("已退出，恢复系统默认电源行为。");

// ============ 类型声明 ============
static class Native
{
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint SetThreadExecutionState(uint esFlags);
}
