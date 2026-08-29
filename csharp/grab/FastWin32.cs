// =====================================================================
// FastWin32.cs —— FastWin32 库独立源码文件(原作者 liang9539/Wwh, 2018)
//   完整复刻 fastwin32 项目全部内容, 含以下命名空间:
//     FastWin32             (NativeMethods/SafeNativeHandle/KBDLLHOOKSTRUCT/...)
//     FastWin32.Asm         (Assembler/AsmData/AsmCompilerException)
//     FastWin32.Diagnostics (Injector/Module32/Process32)
//     FastWin32.Hook        (LocalHook/LocalHookOld/RemoteHook/KeyboardHook/MessageProxy)
//     FastWin32.Memory      (MemoryIO/MemoryManagement/PageInfo/Pointer)
//     FastWin32.Windowing   (Window)
//   本文件为独立只读库源码, 供外部项目引用, 请勿修改。
// =====================================================================
#nullable disable
// usings
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using size_t = System.IntPtr;
using System.Diagnostics;
using static FastWin32.NativeMethods;
using System.IO;
using System.Linq;
using FastWin32.Memory;
using FastWin32.Diagnostics;

// bodies
namespace FastWin32
{
    /// <summary>
    /// 本地方法
    /// </summary>
    internal static unsafe class NativeMethods
    {
        #region Constant
        /// <summary>
        /// 最大模块名长度
        /// </summary>
        public const uint MAX_MODULE_NAME32 = 255;

        /// <summary>
        /// 最大路径长度
        /// </summary>
        public const uint MAX_PATH = 260;

        /// <summary>
        /// 表示当前进程的伪句柄
        /// </summary>
        public static readonly IntPtr CURRENT_PROCESS = (IntPtr)(-1);

        public const uint INFINITE = 0xFFFFFFFF;

        public const uint LIST_MODULES_DEFAULT = 0x0;

        public const uint LIST_MODULES_32BIT = 0x1;

        public const uint LIST_MODULES_64BIT = 0x2;

        public const uint LIST_MODULES_ALL = 0x3;

        #region Hook Id
        /// <summary>
        /// Installs a hook procedure that monitors messages generated as a result of an input event in a dialog box, message box, menu, or scroll bar. For more information, see the MessageProc hook procedure.
        /// </summary>
        public const uint WH_MSGFILTER = unchecked((uint)-1);

        /// <summary>
        /// Installs a hook procedure that records input messages posted to the system message queue. This hook is useful for recording macros. For more information, see the JournalRecordProc hook procedure.
        /// </summary>
        public const uint WH_JOURNALRECORD = 0;

        /// <summary>
        /// Installs a hook procedure that posts messages previously recorded by a WH_JOURNALRECORD hook procedure. For more information, see the JournalPlaybackProc hook procedure.
        /// </summary>
        public const uint WH_JOURNALPLAYBACK = 1;

        /// <summary>
        /// Installs a hook procedure that monitors keystroke messages. For more information, see the KeyboardProc hook procedure.
        /// </summary>
        public const uint WH_KEYBOARD = 2;

        /// <summary>
        /// Installs a hook procedure that monitors messages posted to a message queue. For more information, see the GetMsgProc hook procedure.
        /// </summary>
        public const uint WH_GETMESSAGE = 3;

        /// <summary>
        /// Installs a hook procedure that monitors messages before the system sends them to the destination window procedure. For more information, see the CallWndProc hook procedure.
        /// </summary>
        public const uint WH_CALLWNDPROC = 4;

        /// <summary>
        /// Installs a hook procedure that receives notifications useful to a CBT application. For more information, see the CBTProc hook procedure.
        /// </summary>
        public const uint WH_CBT = 5;

        /// <summary>
        /// Installs a hook procedure that monitors messages generated as a result of an input event in a dialog box, message box, menu, or scroll bar. The hook procedure monitors these messages for all applications in the same desktop as the calling thread. For more information, see the SysMsgProc hook procedure.
        /// </summary>
        public const uint WH_SYSMSGFILTER = 6;

        /// <summary>
        /// Installs a hook procedure that monitors mouse messages. For more information, see the MouseProc hook procedure.
        /// </summary>
        public const uint WH_MOUSE = 7;

        /// <summary>
        /// 当调用 GetMessage 或 PeekMessage 来从消息队列种查询非鼠标、键盘消息时
        /// </summary>
        public const uint WH_HARDWARE = 8;

        /// <summary>
        /// Installs a hook procedure useful for debugging other hook procedures. For more information, see the DebugProc hook procedure.
        /// </summary>
        public const uint WH_DEBUG = 9;

        /// <summary>
        /// Installs a hook procedure that receives notifications useful to shell applications. For more information, see the ShellProc hook procedure.
        /// </summary>
        public const uint WH_SHELL = 10;

        /// <summary>
        /// Installs a hook procedure that will be called when the application's foreground thread is about to become idle. This hook is useful for performing low priority tasks during idle time. For more information, see the ForegroundIdleProc hook procedure.
        /// </summary>
        public const uint WH_FOREGROUNDIDLE = 11;

        /// <summary>
        /// Installs a hook procedure that monitors messages after they have been processed by the destination window procedure. For more information, see the CallWndRetProc hook procedure.
        /// </summary>
        public const uint WH_CALLWNDPROCRET = 12;

        /// <summary>
        /// Installs a hook procedure that monitors low-level keyboard input events. For more information, see the LowLevelKeyboardProc hook procedure.
        /// </summary>
        public const uint WH_KEYBOARD_LL = 13;

        /// <summary>
        /// Installs a hook procedure that monitors low-level mouse input events. For more information, see the LowLevelMouseProc hook procedure.
        /// </summary>
        public const uint WH_MOUSE_LL = 14;
        #endregion

        #region Memory
        public const uint PAGE_NOACCESS = 0x01;

        public const uint PAGE_READONLY = 0x02;

        public const uint PAGE_READWRITE = 0x04;

        public const uint PAGE_WRITECOPY = 0x08;

        public const uint PAGE_EXECUTE = 0x10;

        public const uint PAGE_EXECUTE_READ = 0x20;

        public const uint PAGE_EXECUTE_READWRITE = 0x40;

        public const uint PAGE_EXECUTE_WRITECOPY = 0x80;

        public const uint PAGE_GUARD = 0x100;

        public const uint PAGE_NOCACHE = 0x200;

        public const uint PAGE_WRITECOMBINE = 0x400;

        public const uint PAGE_REVERT_TO_FILE_MAP = 0x80000000;

        public const uint PAGE_ENCLAVE_THREAD_CONTROL = 0x80000000;

        public const uint PAGE_TARGETS_NO_UPDATE = 0x40000000;

        public const uint PAGE_TARGETS_INVALID = 0x40000000;

        public const uint PAGE_ENCLAVE_UNVALIDATED = 0x20000000;

        public const uint MEM_COMMIT = 0x00001000;

        public const uint MEM_RESERVE = 0x00002000;

        public const uint MEM_DECOMMIT = 0x00004000;

        public const uint MEM_RELEASE = 0x00008000;

        public const uint MEM_FREE = 0x00010000;

        public const uint MEM_PRIVATE = 0x00020000;

        public const uint MEM_MAPPED = 0x00040000;

        public const uint MEM_RESET = 0x00080000;

        public const uint MEM_TOP_DOWN = 0x00100000;

        public const uint MEM_WRITE_WATCH = 0x00200000;

        public const uint MEM_PHYSICAL = 0x00400000;

        public const uint MEM_ROTATE = 0x00800000;

        public const uint MEM_DIFFERENT_IMAGE_BASE_OK = 0x00800000;

        public const uint MEM_RESET_UNDO = 0x01000000;

        public const uint MEM_LARGE_PAGES = 0x20000000;

        public const uint MEM_4MB_PAGES = 0x80000000;

        public const uint MEM_64K_PAGES = MEM_LARGE_PAGES | MEM_PHYSICAL;

        public const uint SEC_64K_PAGES = 0x00080000;

        public const uint SEC_FILE = 0x00800000;

        public const uint SEC_IMAGE = 0x01000000;

        public const uint SEC_PROTECTED_IMAGE = 0x02000000;

        public const uint SEC_RESERVE = 0x04000000;

        public const uint SEC_COMMIT = 0x08000000;

        public const uint SEC_NOCACHE = 0x10000000;

        public const uint SEC_WRITECOMBINE = 0x40000000;

        public const uint SEC_LARGE_PAGES = 0x80000000;

        public const uint SEC_IMAGE_NO_EXECUTE = SEC_IMAGE | SEC_NOCACHE;

        public const uint MEM_IMAGE = SEC_IMAGE;

        public const uint WRITE_WATCH_FLAG_RESET = 0x01;

        public const uint MEM_UNMAP_WITH_TRANSIENT_BOOST = 0x01;
        #endregion

        #region Process Security and Access Rights
        /// <summary>
        /// Required to delete the object.
        /// </summary>
        public const uint DELETE = 0x00010000;

        /// <summary>
        /// Required to read information in the security descriptor for the object, not including the information in the SACL.
        /// To read or write the SACL, you must request the ACCESS_SYSTEM_SECURITY access right.
        /// For more information, see SACL Access Right.
        /// </summary>
        public const uint READ_CONTROL = 0x00020000;

        /// <summary>
        /// The right to use the object for synchronization.
        /// This enables a thread to wait until the object is in the signaled state.
        /// </summary>
        public const uint SYNCHRONIZE = 0x00100000;

        /// <summary>
        /// Required to modify the DACL in the security descriptor for the object.
        /// </summary>
        public const uint WRITE_DAC = 0x00040000;

        /// <summary>
        /// Required to change the owner in the security descriptor for the object.
        /// </summary>
        public const uint WRITE_OWNER = 0x00080000;

        /// <summary>
        /// Standard Rights Required
        /// </summary>
        public const uint STANDARD_RIGHTS_REQUIRED = DELETE | READ_CONTROL | WRITE_DAC | WRITE_OWNER;

        /// <summary>
        /// Required to create a process.
        /// </summary>
        public const uint PROCESS_CREATE_PROCESS = 0x0080;

        /// <summary>
        /// Required to create a thread.
        /// </summary>
        public const uint PROCESS_CREATE_THREAD = 0x0002;

        /// <summary>
        /// Required to duplicate a handle using DuplicateHandle.
        /// </summary>
        public const uint PROCESS_DUP_HANDLE = 0x0040;

        /// <summary>
        /// Required to retrieve certain information about a process, such as its token, exit code, and priority class (see OpenProcessToken).
        /// </summary>
        public const uint PROCESS_QUERY_INFORMATION = 0x0400;

        /// <summary>
        /// Required to retrieve certain information about a process (see GetExitCodeProcess, GetPriorityClass, IsProcessInJob, QueryFullProcessImageName).
        /// A handle that has the PROCESS_QUERY_INFORMATION access right is automatically granted PROCESS_QUERY_LIMITED_INFORMATION.
        /// </summary>
        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        /// <summary>
        /// Required to set certain information about a process, such as its priority class (see SetPriorityClass).
        /// </summary>
        public const uint PROCESS_SET_INFORMATION = 0x0200;

        /// <summary>
        /// Required to set memory limits using SetProcessWorkingSetSize.
        /// </summary>
        public const uint PROCESS_SET_QUOTA = 0x0100;

        /// <summary>
        /// Required to suspend or resume a process.
        /// </summary>
        public const uint PROCESS_SUSPEND_RESUME = 0x0800;

        /// <summary>
        /// Required to terminate a process using TerminateProcess.
        /// </summary>
        public const uint PROCESS_TERMINATE = 0x0001;

        /// <summary>
        /// Required to perform an operation on the address space of a process (see VirtualProtectEx and O:WriteProcessMemory).
        /// </summary>
        public const uint PROCESS_VM_OPERATION = 0x0008;

        /// <summary>
        /// Required to read memory in a process using O:ReadProcessMemory.
        /// </summary>
        public const uint PROCESS_VM_READ = 0x0010;

        /// <summary>
        /// Required to write to memory in a process using O:WriteProcessMemory.
        /// </summary>
        public const uint PROCESS_VM_WRITE = 0x0020;

        /// <summary>
        /// All possible access rights for a process object.
        /// </summary>
        public const uint PROCESS_ALL_ACCESS = STANDARD_RIGHTS_REQUIRED | SYNCHRONIZE | 0xFFFF;
        #endregion

        #region Thread Creation
        /// <summary>
        /// 线程被创建为挂起状态
        /// </summary>
        public const uint CREATE_SUSPENDED = 0x00000004;

        /// <summary>
        /// 指定堆栈大小
        /// </summary>
        public const uint STACK_SIZE_PARAM_IS_A_RESERVATION = 0x00010000;
        #endregion

        #region Thread Security and Access Rights
        /// <summary>
        /// Required to read certain information from the thread object, such as the exit code (see GetExitCodeThread).
        /// </summary>
        public const uint THREAD_QUERY_INFORMATION = 0x0040;
        #endregion

        #region Window Messages
        public const uint WM_NULL = 0x0000;

        public const uint WM_KEYDOWN = 0x0100;

        public const uint WM_KEYUP = 0x0101;

        public const uint WM_SYSKEYDOWN = 0x0104;

        public const uint WM_SYSKEYUP = 0x0105;

        public const uint WM_USER = 0x0400;

        public const uint WM_APP = 0x8000;
        #endregion
        #endregion

        #region Structures
        #region Hook Structures
        /// <summary>
        /// Contains information about a low-level keyboard input event.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct KBDLLHOOKSTRUCT
        {
            /// <summary>
            /// A virtual-key code.
            /// The code must be a value in the range 1 to 254.
            /// </summary>
            public uint vkCode;

            /// <summary>
            /// A hardware scan code for the key.
            /// </summary>
            public uint scanCode;

            /// <summary>
            /// The extended-key flag, event-injected flags, context code, and transition-state flag.
            /// This member is specified as follows.
            /// An application can use the following values to test the keystroke flags.
            /// Testing LLKHF_INJECTED (bit 4) will tell you whether the event was injected.
            /// If it was, then testing LLKHF_LOWER_IL_INJECTED (bit 1) will tell you whether or not the event was injected from a process running at lower integrity level.
            /// </summary>
            public uint flags;

            /// <summary>
            /// The time stamp for this message, equivalent to what GetMessageTime would return for this message.
            /// </summary>
            public uint time;

            /// <summary>
            /// Additional information associated with the message.
            /// </summary>
            public size_t dwExtraInfo;
        }
        #endregion

        #region ImageHlp Structures
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct IMAGE_EXPORT_DIRECTORY
        {
            public uint Characteristics;

            public uint TimeDateStamp;

            public ushort MajorVersion;

            public ushort MinorVersion;

            public uint Name;

            public uint Base;

            public uint NumberOfFunctions;

            public uint NumberOfNames;

            public uint AddressOfFunctions;

            public uint AddressOfNames;

            public uint AddressOfNameOrdinals;
        }
        #endregion

        #region Memory Management Structures
        /// <summary>
        /// Contains information about a range of pages in the virtual address space of a process.
        /// The VirtualQuery and VirtualQueryEx functions use this structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MEMORY_BASIC_INFORMATION
        {
            /// <summary>
            /// A pointer to the base address of the region of pages.
            /// </summary>
            public IntPtr BaseAddress;

            /// <summary>
            /// A pointer to the base address of a range of pages allocated by the VirtualAlloc function.
            /// The page pointed to by the BaseAddress member is contained within this allocation range.
            /// </summary>
            public IntPtr AllocationBase;

            /// <summary>
            /// The memory protection option when the region was initially allocated.
            /// This member can be one of the memory protection constants or 0 if the caller does not have access.
            /// </summary>
            public uint AllocationProtect;

            /// <summary>
            /// The size of the region beginning at the base address in which all pages have identical attributes, in bytes.
            /// </summary>
            public size_t RegionSize;

            /// <summary>
            /// The state of the pages in the region.
            /// </summary>
            public uint State;

            /// <summary>
            /// The access protection of the pages in the region. This member is one of the values listed for the AllocationProtect member.
            /// </summary>
            public uint Protect;

            /// <summary>
            /// The type of pages in the region.
            /// </summary>
            public uint Type;

            /// <summary>
            /// 结构体在非托管内存中大小
            /// </summary>
            public static readonly uint UnmanagedSize = (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION));
        }
        #endregion

        #region Message Structures
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MSG
        {
            public IntPtr hwnd;

            public uint message;

            public size_t wParam;

            public size_t lParam;

            public uint time;

            public POINT pt;
        }
        #endregion

        #region Rectangle Structures
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct POINT
        {
            public int x;

            public int y;
        }
        #endregion

        #region Shell Structures
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHELLEXECUTEINFO
        {
            public uint cbSize;

            public uint fMask;

            public IntPtr hwnd;

            public string lpVerb;

            public string lpFile;

            public string lpParameters;

            public string lpDirectory;

            public int nShow;

            public IntPtr hInstApp;

            public IntPtr lpIDList;

            public string lpClass;

            public IntPtr hkeyClass;

            public uint dwHotKey;

            public IntPtr hMonitor;

            public IntPtr hProcess;

            public static readonly uint UnmanagedSize = (uint)Marshal.SizeOf(typeof(SHELLEXECUTEINFO));
        }
        #endregion
        #endregion

        #region Callback
        /// <summary>
        /// 回调函数 要继续遍历,返回true;要停止遍历,返回false
        /// </summary>
        /// <param name="hWnd">子级窗口的句柄</param>
        /// <param name="lParam">EnumWindows或EnumDesktopWindows中给出的应用程序定义值</param>
        /// <returns></returns>
        [return: MarshalAs(UnmanagedType.Bool)]
        public delegate bool EnumChildProc(IntPtr hWnd, size_t lParam);

        /// <summary>
        /// 回调函数 要继续遍历,返回true;要停止遍历,返回false
        /// </summary>
        /// <param name="hWnd">顶级窗口的句柄</param>
        /// <param name="lParam">EnumWindows或EnumDesktopWindows中给出的应用程序定义值</param>
        /// <returns></returns>
        [return: MarshalAs(UnmanagedType.Bool)]
        public delegate bool EnumWindowsProc(IntPtr hWnd, size_t lParam);

        /// <summary>
        /// HookProc 回调函数
        /// </summary>
        /// <param name="nCode">钩子代码传递给当前的钩子过程。下一个钩子过程使用此代码来确定如何处理挂钩信息。</param>
        /// <param name="wParam">此参数的含义取决于与当前钩链相关联的钩子类型。</param>
        /// <param name="lParam">此参数的含义取决于与当前钩链相关联的钩子类型。</param>
        /// <returns></returns>
        public delegate IntPtr HookProc(int nCode, size_t wParam, size_t lParam);
        #endregion

        #region Functions
        #region Debugging Functions
        #region ReadProcessMemory
        /// <summary>
        /// 读取内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要读取的内容</param>
        /// <param name="nSize">读取内容的大小</param>
        /// <param name="lpNumberOfBytesRead">实际读取大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "ReadProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, size_t nSize, size_t* lpNumberOfBytesRead);

        /// <summary>
        /// 读取内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要读取的内容</param>
        /// <param name="nSize">读取内容的大小</param>
        /// <param name="lpNumberOfBytesRead">实际读取大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "ReadProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, size_t nSize, out size_t lpNumberOfBytesRead);

        /// <summary>
        /// 读取内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要读取的内容</param>
        /// <param name="nSize">读取内容的大小</param>
        /// <param name="lpNumberOfBytesRead">实际读取大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "ReadProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, void* lpBuffer, size_t nSize, size_t* lpNumberOfBytesRead);

        /// <summary>
        /// 读取内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要读取的内容</param>
        /// <param name="nSize">读取内容的大小</param>
        /// <param name="lpNumberOfBytesRead">实际读取大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "ReadProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, out byte lpBuffer, size_t nSize, size_t* lpNumberOfBytesRead);

        /// <summary>
        /// 读取内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要读取的内容</param>
        /// <param name="nSize">读取内容的大小</param>
        /// <param name="lpNumberOfBytesRead">实际读取大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "ReadProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, out bool lpBuffer, size_t nSize, size_t* lpNumberOfBytesRead);

        /// <summary>
        /// 读取内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要读取的内容</param>
        /// <param name="nSize">读取内容的大小</param>
        /// <param name="lpNumberOfBytesRead">实际读取大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "ReadProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, out char lpBuffer, size_t nSize, size_t* lpNumberOfBytesRead);

        /// <summary>
        /// 读取内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要读取的内容</param>
        /// <param name="nSize">读取内容的大小</param>
        /// <param name="lpNumberOfBytesRead">实际读取大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "ReadProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, out short lpBuffer, size_t nSize, size_t* lpNumberOfBytesRead);

        /// <summary>
        /// 读取内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要读取的内容</param>
        /// <param name="nSize">读取内容的大小</param>
        /// <param name="lpNumberOfBytesRead">实际读取大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "ReadProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, out ushort lpBuffer, size_t nSize, size_t* lpNumberOfBytesRead);

        /// <summary>
        /// 读取内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要读取的内容</param>
        /// <param name="nSize">读取内容的大小</param>
        /// <param name="lpNumberOfBytesRead">实际读取大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "ReadProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, out int lpBuffer, size_t nSize, size_t* lpNumberOfBytesRead);

        /// <summary>
        /// 读取内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要读取的内容</param>
        /// <param name="nSize">读取内容的大小</param>
        /// <param name="lpNumberOfBytesRead">实际读取大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "ReadProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, out uint lpBuffer, size_t nSize, size_t* lpNumberOfBytesRead);

        /// <summary>
        /// 读取内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要读取的内容</param>
        /// <param name="nSize">读取内容的大小</param>
        /// <param name="lpNumberOfBytesRead">实际读取大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "ReadProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, out long lpBuffer, size_t nSize, size_t* lpNumberOfBytesRead);

        /// <summary>
        /// 读取内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要读取的内容</param>
        /// <param name="nSize">读取内容的大小</param>
        /// <param name="lpNumberOfBytesRead">实际读取大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "ReadProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, out ulong lpBuffer, size_t nSize, size_t* lpNumberOfBytesRead);

        /// <summary>
        /// 读取内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要读取的内容</param>
        /// <param name="nSize">读取内容的大小</param>
        /// <param name="lpNumberOfBytesRead">实际读取大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "ReadProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, out float lpBuffer, size_t nSize, size_t* lpNumberOfBytesRead);

        /// <summary>
        /// 读取内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要读取的内容</param>
        /// <param name="nSize">读取内容的大小</param>
        /// <param name="lpNumberOfBytesRead">实际读取大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "ReadProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, out double lpBuffer, size_t nSize, size_t* lpNumberOfBytesRead);

        /// <summary>
        /// 读取内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要读取的内容</param>
        /// <param name="nSize">读取内容的大小</param>
        /// <param name="lpNumberOfBytesRead">实际读取大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "ReadProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, out IntPtr lpBuffer, size_t nSize, size_t* lpNumberOfBytesRead);
        #endregion

        #region WriteProcessMemory
        /// <summary>
        /// 写入内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要写入的内容</param>
        /// <param name="nSize">写入内容的大小</param>
        /// <param name="lpNumberOfBytesWritten">实际写入大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "WriteProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, size_t nSize, size_t* lpNumberOfBytesWritten);

        /// <summary>
        /// 写入内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要写入的内容</param>
        /// <param name="nSize">写入内容的大小</param>
        /// <param name="lpNumberOfBytesWritten">实际写入大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "WriteProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, size_t nSize, out size_t lpNumberOfBytesWritten);

        /// <summary>
        /// 写入内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要写入的内容</param>
        /// <param name="nSize">写入内容的大小</param>
        /// <param name="lpNumberOfBytesWritten">实际写入大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "WriteProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, void* lpBuffer, size_t nSize, size_t* lpNumberOfBytesWritten);

        /// <summary>
        /// 写入内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要写入的内容</param>
        /// <param name="nSize">写入内容的大小</param>
        /// <param name="lpNumberOfBytesWritten">实际写入大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "WriteProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, ref bool lpBuffer, size_t nSize, size_t* lpNumberOfBytesWritten);

        /// <summary>
        /// 写入内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要写入的内容</param>
        /// <param name="nSize">写入内容的大小</param>
        /// <param name="lpNumberOfBytesWritten">实际写入大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "WriteProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, ref byte lpBuffer, size_t nSize, size_t* lpNumberOfBytesWritten);

        /// <summary>
        /// 写入内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要写入的内容</param>
        /// <param name="nSize">写入内容的大小</param>
        /// <param name="lpNumberOfBytesWritten">实际写入大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "WriteProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, ref char lpBuffer, size_t nSize, size_t* lpNumberOfBytesWritten);

        /// <summary>
        /// 写入内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要写入的内容</param>
        /// <param name="nSize">写入内容的大小</param>
        /// <param name="lpNumberOfBytesWritten">实际写入大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "WriteProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, ref short lpBuffer, size_t nSize, size_t* lpNumberOfBytesWritten);

        /// <summary>
        /// 写入内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要写入的内容</param>
        /// <param name="nSize">写入内容的大小</param>
        /// <param name="lpNumberOfBytesWritten">实际写入大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "WriteProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, ref ushort lpBuffer, size_t nSize, size_t* lpNumberOfBytesWritten);

        /// <summary>
        /// 写入内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要写入的内容</param>
        /// <param name="nSize">写入内容的大小</param>
        /// <param name="lpNumberOfBytesWritten">实际写入大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "WriteProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, ref int lpBuffer, size_t nSize, size_t* lpNumberOfBytesWritten);

        /// <summary>
        /// 写入内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要写入的内容</param>
        /// <param name="nSize">写入内容的大小</param>
        /// <param name="lpNumberOfBytesWritten">实际写入大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "WriteProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, ref uint lpBuffer, size_t nSize, size_t* lpNumberOfBytesWritten);

        /// <summary>
        /// 写入内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要写入的内容</param>
        /// <param name="nSize">写入内容的大小</param>
        /// <param name="lpNumberOfBytesWritten">实际写入大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "WriteProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, ref long lpBuffer, size_t nSize, size_t* lpNumberOfBytesWritten);

        /// <summary>
        /// 写入内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要写入的内容</param>
        /// <param name="nSize">写入内容的大小</param>
        /// <param name="lpNumberOfBytesWritten">实际写入大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "WriteProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, ref ulong lpBuffer, size_t nSize, size_t* lpNumberOfBytesWritten);

        /// <summary>
        /// 写入内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要写入的内容</param>
        /// <param name="nSize">写入内容的大小</param>
        /// <param name="lpNumberOfBytesWritten">实际写入大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "WriteProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, ref float lpBuffer, size_t nSize, size_t* lpNumberOfBytesWritten);

        /// <summary>
        /// 写入内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要写入的内容</param>
        /// <param name="nSize">写入内容的大小</param>
        /// <param name="lpNumberOfBytesWritten">实际写入大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "WriteProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, ref double lpBuffer, size_t nSize, size_t* lpNumberOfBytesWritten);

        /// <summary>
        /// 写入内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要写入的内容</param>
        /// <param name="nSize">写入内容的大小</param>
        /// <param name="lpNumberOfBytesWritten">实际写入大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "WriteProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, ref IntPtr lpBuffer, size_t nSize, size_t* lpNumberOfBytesWritten);

        /// <summary>
        /// 写入内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpBaseAddress">地址</param>
        /// <param name="lpBuffer">要写入的内容</param>
        /// <param name="nSize">写入内容的大小</param>
        /// <param name="lpNumberOfBytesWritten">实际写入大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "WriteProcessMemory", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, string lpBuffer, size_t nSize, size_t* lpNumberOfBytesWritten);
        #endregion
        #endregion

        #region Dynamic-Link Library Functions
        /// <summary>
        /// 获取模块所在路径
        /// </summary>
        /// <param name="hModule">模块句柄</param>
        /// <param name="lpFilename">文件路径</param>
        /// <param name="nSize">缓冲区大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "GetModuleFileNameW", ExactSpelling = true, SetLastError = true)]
        public static extern uint GetModuleFileName(IntPtr hModule, StringBuilder lpFilename, uint nSize);

        /// <summary>
        /// 获取当前进程中符合条件的模块句柄
        /// </summary>
        /// <param name="lpModuleName">模块名</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "GetModuleHandleW", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        /// <summary>
        /// 获取指定模块中导出函数的地址
        /// </summary>
        /// <param name="hModule">模块句柄</param>
        /// <param name="lpProcName">函数名</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Ansi, EntryPoint = "GetProcAddress", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string lpProcName);

        /// <summary>
        /// 加载DLL
        /// </summary>
        /// <param name="lpFileName">DLL路径</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "LoadLibraryW", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr LoadLibrary(string lpFileName);
        #endregion

        #region Hook Functions
        /// <summary>
        /// 安装Windows消息钩子
        /// </summary>
        /// <param name="idHook">将安装的钩子的类型</param>
        /// <param name="lpfn">回调函数</param>
        /// <param name="hMod">回调函数所在模块的句柄。如果 dwThreadId 指定的线程由当前进程创建并且回调函数在当前进程中，参数必须设置为 IntPtr.Zero</param>
        /// <param name="dwThreadId">与回调函数关联的线程ID，0为全局钩子</param>
        /// <returns>返回钩子的句柄</returns>
        [DllImport("user32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "SetWindowsHookExW", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(uint idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        /// <summary>
        /// 卸载Windows消息钩子
        /// </summary>
        /// <param name="hhk">要卸载的钩子的句柄</param>
        /// <returns></returns>
        [DllImport("user32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "UnhookWindowsHookEx", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        /// <summary>
        /// 将钩子信息传递给当前钩子链中的下一个钩子过程。挂钩过程可以在处理挂钩信息之前或之后调用此函数。
        /// </summary>
        /// <param name="hhk">当前钩子的句柄，可以不填写</param>
        /// <param name="nCode">钩子代码传递给当前的钩子过程。下一个钩子过程使用此代码来确定如何处理挂钩信息。</param>
        /// <param name="wParam">所述的wParam传递给当前挂钩过程值。此参数的含义取决于与当前钩链相关联的钩子类型。</param>
        /// <param name="lParam">所述的lParam传递给当前挂钩过程值。此参数的含义取决于与当前钩链相关联的钩子类型。</param>
        /// <returns>该值由链中的下一个钩子过程返回。当前的钩子过程也必须返回此值。返回值的含义取决于钩子类型。有关详细信息，请参阅各个挂钩过程的说明。</returns>
        [DllImport("user32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "CallNextHookEx", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, size_t wParam, size_t lParam);
        #endregion

        #region Keyboard Input Functions
        /// <summary>
        /// 将虚拟键的状态拷贝到缓冲区
        /// </summary>
        /// <param name="lpKeyState">指向一个256字节的数组，数组用于接收每个虚拟键的状态。</param>
        /// <returns></returns>
        [DllImport("user32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "GetKeyboardState", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetKeyboardState(byte[] lpKeyState);

        /// <summary>
        /// 获取虚拟键状态
        /// </summary>
        /// <param name="nVirtKey"></param>
        /// <returns>高位为1，表示按下，为0表示未按下。低位为1，表示虚拟键被切换。比如按下Caps Lock键，低位为1，反之低位为0</returns>
        [DllImport("user32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "GetKeyState", ExactSpelling = true, SetLastError = true)]
        public static extern short GetKeyState(int nVirtKey);

        /// <summary>
        /// 该函数将指定的虚拟键码和键盘状态翻译为相应的字符或字符串。该函数使用由给定的键盘布局句柄标识的物理键盘布局和输入语言来翻译代码。
        /// </summary>
        /// <param name="uVirtKey">指定要翻译的虚拟键码。</param>
        /// <param name="uScanCode">定义被翻译键的硬件扫描码。若该键处于Up状态，则该值的最高位被设置。</param>
        /// <param name="lpKeyState">指向包含当前键盘状态的一个256字节数组。数组的每个成员包含一个键的状态。若某字节的最高位被设置，则该键处于Down状态。若最低位被设置，则表明该键被触发。在此函数中，仅有Caps Lock键的触发位是相关的。Num Lock和Scroll Lock键的触发状态将被忽略。</param>
        /// <param name="lpChar">指向接受翻译所得字符或字符串的缓冲区。</param>
        /// <param name="uFlags">定义一个菜单是否处于激活状态。若一菜单是活动的，则该参数为1，否则为0。</param>
        /// <returns></returns>
        [DllImport("user32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "ToAscii", ExactSpelling = true, SetLastError = true)]
        public static extern int ToAscii(uint uVirtKey, uint uScanCode, byte[] lpKeyState, out char lpChar, uint uFlags);
        #endregion

        #region Memory Management Functions
        /// <summary>
        /// 在当前进程中分配内存
        /// </summary>
        /// <param name="lpAddress">指定一个地址用于分配内存（如果为IntPtr.Zero则自动分配）</param>
        /// <param name="dwSize">要分配内存的大小</param>
        /// <param name="flAllocationType">内存分配选项</param>
        /// <param name="flProtect">内存保护选项</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "VirtualAlloc", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr VirtualAlloc(IntPtr lpAddress, size_t dwSize, uint flAllocationType, uint flProtect);

        /// <summary>
        /// 分配内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpAddress">指定一个地址用于分配内存（如果为IntPtr.Zero则自动分配）</param>
        /// <param name="dwSize">要分配内存的大小</param>
        /// <param name="flAllocationType">内存分配选项</param>
        /// <param name="flProtect">内存保护选项</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "VirtualAllocEx", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, size_t dwSize, uint flAllocationType, uint flProtect);

        /// <summary>
        /// 在当前进程中释放内存
        /// </summary>
        /// <param name="lpAddress">指定释放内存的地址</param>
        /// <param name="dwSize">要释放内存的大小</param>
        /// <param name="dwFreeType">内存释放选项</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "VirtualFree", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool VirtualFree(IntPtr lpAddress, size_t dwSize, uint dwFreeType);

        /// <summary>
        /// 释放内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpAddress">指定释放内存的地址</param>
        /// <param name="dwSize">要释放内存的大小</param>
        /// <param name="dwFreeType">内存释放选项</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "VirtualFreeEx", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, size_t dwSize, uint dwFreeType);

        /// <summary>
        /// 查询地址空间中内存地址的信息
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpAddress">查询内存的地址</param>
        /// <param name="lpBuffer">内存页面信息</param>
        /// <param name="dwLength">MEMORY_BASIC_INFORMATION结构的大小</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "VirtualQueryEx", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, size_t dwLength);
        #endregion

        #region Message Functions
        /// <summary>
        /// 向线程发送消息
        /// </summary>
        /// <param name="idThread">线程ID</param>
        /// <param name="Msg">消息类型</param>
        /// <param name="wParam">参数1</param>
        /// <param name="lParam">参数2</param>
        /// <returns></returns>
        [DllImport("user32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "PostThreadMessageW", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PostThreadMessage(uint idThread, uint Msg, size_t wParam, size_t lParam);

        /// <summary>
        /// 同步方法发送消息
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="Msg">消息</param>
        /// <param name="wParam">参数1</param>
        /// <param name="lParam">参数2</param>
        /// <returns></returns>
        [DllImport("user32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "SendMessageW", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, size_t wParam, size_t lParam);
        #endregion

        #region Process and Thread Functions
        /// <summary>
        /// 将一个线程的输入处理机制附加或分离到另一个线程的输入处理机制
        /// </summary>
        /// <param name="idAttach">附加线程的ID，不能是系统线程</param>
        /// <param name="idAttachTo">被附加线程的ID，不能是系统线程</param>
        /// <param name="fAttach">true为附加，false为分离</param>
        /// <returns></returns>
        [DllImport("user32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "AttachThreadInput", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        /// <summary>
        /// 关闭句柄
        /// </summary>
        /// <param name="hObject">句柄</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "CloseHandle", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        /// <summary>
        /// 创建远程线程
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpThreadAttributes"></param>
        /// <param name="dwStackSize">堆栈初始大小，如果为0，则使用系统默认</param>
        /// <param name="lpStartAddress">远程进程中线程的起始地址</param>
        /// <param name="lpParameter">指向要传递给线程函数的变量的指针</param>
        /// <param name="dwCreationFlags">线程创建的标志</param>
        /// <param name="lpThreadId">指向接收线程标识符的变量的指针</param>
        /// <returns>如果函数成功，返回值是新线程的句柄，否则返回值是IntPtr.Zero</returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "CreateRemoteThread", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr CreateRemoteThread(IntPtr hProcess, void* lpThreadAttributes, size_t dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, uint* lpThreadId);

        /// <summary>
        /// 获取当前进程ID
        /// </summary>
        /// <returns>线程ID</returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "GetCurrentProcessId", ExactSpelling = true, SetLastError = true)]
        public static extern uint GetCurrentProcessId();

        /// <summary>
        /// 获取当前线程ID
        /// </summary>
        /// <returns>线程ID</returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "GetCurrentThreadId", ExactSpelling = true, SetLastError = true)]
        public static extern uint GetCurrentThreadId();

        /// <summary>
        /// Retrieves the termination status of the specified thread.
        /// </summary>
        /// <param name="hThread">A handle to the thread.</param>
        /// <param name="lpExitCode">A pointer to a variable to receive the thread termination status.</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "GetExitCodeThread", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);

        /// <summary>
        /// 获取线程所在进程的ID
        /// </summary>
        /// <param name="Thread">线程句柄</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "GetProcessIdOfThread", ExactSpelling = true, SetLastError = true)]
        public static extern uint GetProcessIdOfThread(IntPtr Thread);

        /// <summary>
        /// 获取指定的进程是否在WOW64下运行
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="Wow64Process">
        /// 32位进程运行在32位Windows下：false
        /// 32位进程运行在64位Windows下：true
        /// 64位进程运行在64位Windows下：false
        /// </param>
        /// <returns>返回值是函数是否执行成功，而不是是否为64位进程！！！</returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "IsWow64Process", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWow64Process(IntPtr hProcess, [MarshalAs(UnmanagedType.Bool)] out bool Wow64Process);

        /// <summary>
        /// 打开进程
        /// </summary>
        /// <param name="dwDesiredAccess">权限</param>
        /// <param name="bInheritHandle">是否继承</param>
        /// <param name="dwProcessId">进程ID</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "OpenProcess", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        /// <summary>
        /// 打开线程
        /// </summary>
        /// <param name="dwDesiredAccess">权限</param>
        /// <param name="bInheritHandle">是否继承</param>
        /// <param name="dwThreadId">线程ID</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "OpenThread", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

        /// <summary>
        /// 打开进程
        /// </summary>
        /// <param name="dwDesiredAccess">权限</param>
        /// <param name="bInheritHandle">是否继承</param>
        /// <param name="dwProcessId">进程ID</param>
        /// <returns></returns>
        public static SafeNativeHandle SafeOpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId) => OpenProcess(dwDesiredAccess, bInheritHandle, dwProcessId);

        /// <summary>
        /// 打开线程
        /// </summary>
        /// <param name="dwDesiredAccess">权限</param>
        /// <param name="bInheritHandle">是否继承</param>
        /// <param name="dwThreadId">线程ID</param>
        /// <returns></returns>
        public static SafeNativeHandle SafeOpenThread(uint dwDesiredAccess, bool bInheritHandle, uint dwThreadId) => OpenThread(dwDesiredAccess, bInheritHandle, dwThreadId);
        #endregion

        #region PSAPI Functions
        /// <summary>
        /// 遍历所有进程ID
        /// </summary>
        /// <param name="pProcessIds">进程ID</param>
        /// <param name="cb"></param>
        /// <param name="pBytesReturned"></param>
        /// <returns></returns>
        [DllImport("psapi.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "EnumProcesses", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumProcesses(ref uint pProcessIds, uint cb, out uint pBytesReturned);

        /// <summary>
        /// 遍历进程的所有模块
        /// </summary>
        /// <param name="hProcess">进程的句柄</param>
        /// <param name="lphModule">模块句柄</param>
        /// <param name="cb">储存模块句柄的字节数</param>
        /// <param name="lpcbNeeded">储存所有模块句柄所需的字节数</param>
        /// <returns></returns>
        [DllImport("psapi.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "EnumProcessModules", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumProcessModules(IntPtr hProcess, IntPtr* lphModule, uint cb, out uint lpcbNeeded);

        /// <summary>
        /// 遍历进程的所有模块
        /// </summary>
        /// <param name="hProcess">进程的句柄</param>
        /// <param name="lphModule">模块句柄</param>
        /// <param name="cb">储存模块句柄的字节数</param>
        /// <param name="lpcbNeeded">储存所有模块句柄所需的字节数</param>
        /// <param name="dwFilterFlag">过滤条件</param>
        /// <returns></returns>
        [DllImport("psapi.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "EnumProcessModulesEx", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumProcessModulesEx(IntPtr hProcess, IntPtr* lphModule, uint cb, out uint lpcbNeeded, uint dwFilterFlag);

        /// <summary>
        /// 获取模块名
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="hModule">模块句柄</param>
        /// <param name="lpBaseName">模块名</param>
        /// <param name="nSize">最大模块名长度</param>
        /// <returns>成功将返回非零整数</returns>
        [DllImport("psapi.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "GetModuleBaseNameW", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetModuleBaseName(IntPtr hProcess, IntPtr hModule, StringBuilder lpBaseName, uint nSize);

        /// <summary>
        /// 获取进程路径
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <param name="lpImageFileName">进程路径</param>
        /// <param name="nSize">缓存区大小</param>
        /// <returns></returns>
        [DllImport("psapi.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "GetProcessImageFileNameW", ExactSpelling = true, SetLastError = true)]
        public static extern uint GetProcessImageFileName(IntPtr hProcess, StringBuilder lpImageFileName, uint nSize);
        #endregion

        #region Shell Functions
        /// <summary>
        /// 启动程序
        /// </summary>
        /// <param name="pExecInfo">选项</param>
        /// <returns></returns>
        [DllImport("shell32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "ShellExecuteExW", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO pExecInfo);
        #endregion

        #region Synchronization Functions
        /// <summary>
        /// 等待对象被关闭
        /// </summary>
        /// <param name="hHandle">对象的句柄</param>
        /// <param name="dwMilliseconds">最多等待多少毫秒</param>
        /// <returns></returns>
        [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "WaitForSingleObject", ExactSpelling = true, SetLastError = true)]
        public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
        #endregion

        #region Window Functions
        /// <summary>
        /// 遍历所有子窗口
        /// </summary>
        /// <param name="hWndParent">父窗口句柄</param>
        /// <param name="lpEnumFunc"></param>
        /// <param name="lParam"></param>
        /// <returns></returns>
        [DllImport("user32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "EnumChildWindows", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, size_t lParam);

        /// <summary>
        /// 遍历所有顶级窗口
        /// </summary>
        /// <param name="lpEnumFunc"></param>
        /// <param name="lParam">自定义参数</param>
        /// <returns></returns>
        [DllImport("user32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "EnumWindows", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, size_t lParam);

        /// <summary>
        /// 查找窗口
        /// </summary>
        /// <param name="lpClassName">窗口类名</param>
        /// <param name="lpWindowName">窗口标题</param>
        /// <returns></returns>
        [DllImport("user32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "FindWindowW", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        /// <summary>
        /// 查找窗口
        /// </summary>
        /// <param name="hWndParent">父窗口句柄</param>
        /// <param name="hWndChildAfter">从此窗口之后开始查找（此窗口必须为父窗口的直接子窗口）</param>
        /// <param name="lpszClass">窗口类名</param>
        /// <param name="lpszWindow">窗口标题</param>
        /// <returns></returns>
        [DllImport("user32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "FindWindowExW", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string lpszWindow);

        /// <summary>
        /// 获取当前顶端窗口
        /// </summary>
        /// <returns></returns>
        [DllImport("user32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "GetForegroundWindow", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr GetForegroundWindow();

        /// <summary>
        /// 获取Program Manager的窗口句柄
        /// </summary>
        /// <returns></returns>
        [DllImport("user32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "GetShellWindow", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr GetShellWindow();

        /// <summary>
        /// 获取某个窗口的创建者的线程ID和进程ID
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="lpdwProcessId">进程ID</param>
        /// <returns>线程ID</returns>
        [DllImport("user32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "GetWindowThreadProcessId", ExactSpelling = true, SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, uint* lpdwProcessId);

        /// <summary>
        /// 是否为有效窗口
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <returns></returns>
        [DllImport("user32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "IsWindow", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr hWnd);

        /// <summary>
        /// 设置窗口位置
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="hWndInsertAfter">窗口Z序</param>
        /// <param name="x">左上顶点x坐标</param>
        /// <param name="y">左上顶点y坐标</param>
        /// <param name="cx">长度</param>
        /// <param name="cy">高度</param>
        /// <param name="uFlags">选项</param>
        /// <returns></returns>
        [DllImport("user32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "SetWindowPos", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        /// <summary>
        /// 激活窗口
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <returns></returns>
        [DllImport("user32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "SetActiveWindow", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetActiveWindow(IntPtr hWnd);

        /// <summary>
        /// 设置焦点
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <returns></returns>
        [DllImport("user32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "SetFocus", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr SetFocus(IntPtr hWnd);

        /// <summary>
        /// 窗口置顶
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <returns></returns>
        [DllImport("user32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "SetForegroundWindow", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        #endregion

        #region ntdll.dll
        /// <summary>
        /// 恢复进程
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <returns>If the function succeeds, the return value is the thread's previous suspend count.If the function fails, the return value is (DWORD) -1. To get extended error information, call GetLastError.</returns>
        [DllImport("ntdll.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "ZwResumeProcess", ExactSpelling = true, SetLastError = true)]
        public static extern uint ZwResumeProcess(IntPtr hProcess);

        /// <summary>
        /// 暂停进程
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <returns>If the function succeeds, the return value is the thread's previous suspend count; otherwise, it is (DWORD) -1. To get extended error information, use the GetLastError function.</returns>
        [DllImport("ntdll.dll", BestFitMapping = false, CharSet = CharSet.Unicode, EntryPoint = "ZwSuspendProcess", ExactSpelling = true, SetLastError = true)]
        public static extern uint ZwSuspendProcess(IntPtr hProcess);
        #endregion
        #endregion

        /// <summary>
        /// 获取所有不支持的本地方法，可以通过反射调用此方法
        /// <code>string[] notSupportedMethod = (string[])Assembly.Load("FastWin32").GetType("FastWin32.NativeMethods").GetMethod("SelfCheck", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);</code>
        /// </summary>
        /// <returns></returns>
        public static string[] SelfCheck()
        {
            List<string> methodList;
            Type dllImportType;
            DllImportAttribute[] dllImportAttributes;
            IntPtr moduleHandle;

            methodList = new List<string>();
            dllImportType = typeof(DllImportAttribute);
            foreach (MethodInfo methodInfo in typeof(NativeMethods).GetMethods())
            {
                dllImportAttributes = (DllImportAttribute[])methodInfo.GetCustomAttributes(dllImportType, false);
                if (dllImportAttributes == null || dllImportAttributes.Length == 0)
                    continue;
                moduleHandle = LoadLibrary(dllImportAttributes[0].Value);
                if (moduleHandle == IntPtr.Zero)
                    methodList.Add(dllImportAttributes[0].EntryPoint);
                if (GetProcAddress(moduleHandle, dllImportAttributes[0].EntryPoint) == IntPtr.Zero)
                    methodList.Add(dllImportAttributes[0].EntryPoint);
            }
            return methodList.ToArray();
        }
    }
}

namespace FastWin32
{
    /// <summary>
    /// 全局设置
    /// </summary>
    public static class FastWin32Settings
    {
        /// <summary>
        /// 确定当前操作系统是否为 64 位操作系统。
        /// </summary>
        public static readonly bool Is64BitOperatingSystem = Environment.Is64BitOperatingSystem;

        /// <summary>
        /// SeDebugPrivilege特权
        /// </summary>
        public static bool SeDebugPrivilege { get; private set; }

        /// <summary>
        /// 将SeDebugPrivilege特权赋予当前进程
        /// </summary>
        /// <returns></returns>
        public static bool EnableDebugPrivilege()
        {
            if (SeDebugPrivilege)
                return true;
            try
            {
                Process.EnterDebugMode();
                SeDebugPrivilege = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 取消当前进程的SeDebugPrivilege特权
        /// </summary>
        /// <returns></returns>
        public static bool DisableDebugPrivilege()
        {
            if (!SeDebugPrivilege)
                return true;
            try
            {
                Process.LeaveDebugMode();
                SeDebugPrivilege = false;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

namespace FastWin32
{
    /// <summary>
    /// 安全句柄
    /// </summary>
    internal struct SafeNativeHandle : IDisposable
    {
        private IntPtr _handle;

        private bool _isDisposed;

        public bool IsValid => _handle != IntPtr.Zero;

        public static implicit operator SafeNativeHandle(IntPtr value) => new SafeNativeHandle() { _handle = value };

        public static implicit operator IntPtr(SafeNativeHandle value) => value._handle;

        public void Dispose()
        {
            if (_isDisposed)
                return;

            if (_handle != CURRENT_PROCESS)
                CloseHandle(_handle);
            _isDisposed = true;
        }
    }
}

namespace FastWin32.Diagnostics
{
    /// <summary>
    /// 进程
    /// </summary>
    public static unsafe class Process32
    {
        /// <summary>
        /// 打开进程（内存读+查询）
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns></returns>
        private static SafeNativeHandle OpenProcessQuery(uint processId)
        {
            return SafeOpenProcess(FastWin32Settings.SeDebugPrivilege ? PROCESS_ALL_ACCESS : PROCESS_QUERY_INFORMATION, false, processId);
        }

        /// <summary>
        /// 打开进程（进程挂起/恢复）
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns></returns>
        private static SafeNativeHandle OpenProcessProcessSuspendResume(uint processId)
        {
            return SafeOpenProcess(FastWin32Settings.SeDebugPrivilege ? PROCESS_ALL_ACCESS : PROCESS_SUSPEND_RESUME, false, processId);
        }

        /// <summary>
        /// 通过窗口句柄获取进程ID
        /// </summary>
        /// <param name="windowHandle"></param>
        /// <returns></returns>
        public static uint GetProcessIdByWindowHandle(IntPtr windowHandle)
        {
            uint processId;

            GetWindowThreadProcessId(windowHandle, &processId);
            return processId;
        }

        /// <summary>
        /// 通过线程ID获取进程ID
        /// </summary>
        /// <param name="threadId">线程ID</param>
        /// <returns></returns>
        public static uint GetProcessIdByThreadId(uint threadId)
        {
            SafeNativeHandle threadHandle;

            using (threadHandle = SafeOpenThread(THREAD_QUERY_INFORMATION, false, threadId))
                if (threadHandle.IsValid)
                    return GetProcessIdOfThread(threadHandle);
                else
                    return 0;
        }

        /// <summary>
        /// 获取当前进程ID
        /// </summary>
        /// <returns></returns>
        public static uint GetCurrentProcessId()
        {
            return NativeMethods.GetCurrentProcessId();
        }

        /// <summary>
        /// 获取所有进程ID，失败返回null
        /// </summary>
        /// <returns></returns>
        public static uint[] GetAllProcessIds()
        {
            uint[] processIds;
            uint bytesReturned;

            processIds = null;
            do
            {
                if (processIds == null)
                    processIds = new uint[0x200];
                else
                    processIds = new uint[processIds.Length * 2];
                if (!EnumProcesses(ref processIds[0], (uint)(processIds.Length * 4), out bytesReturned))
                    return null;
            } while (bytesReturned == processIds.Length * 4);
            return processIds.Take((int)bytesReturned / 4).ToArray();
        }

        /// <summary>
        /// 获取进程名
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns></returns>
        public static string GetProcessName(uint processId)
        {
            SafeNativeHandle processHandle;

            using (processHandle = OpenProcessQuery(processId))
                if (processHandle.IsValid)
                    return GetProcessNameInternal(processHandle);
                else
                    return null;
        }

        /// <summary>
        /// 获取进程名
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <returns></returns>
        internal static string GetProcessNameInternal(IntPtr processHandle)
        {
            StringBuilder filePath;

            filePath = new StringBuilder((int)MAX_MODULE_NAME32);
            if (GetProcessImageFileName(processHandle, filePath, (int)MAX_MODULE_NAME32) == 0)
                return null;
            return Path.GetFileName(filePath.ToString());
        }

        /// <summary>
        /// 获取进程路径
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns></returns>
        public static string GetProcessPath(uint processId)
        {
            SafeNativeHandle processHandle;

            using (processHandle = OpenProcessQuery(processId))
                if (processHandle.IsValid)
                    return GetProcessPathInternal(processHandle);
                else
                    return null;
        }

        /// <summary>
        /// 获取进程路径
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <returns></returns>
        internal static string GetProcessPathInternal(IntPtr processHandle)
        {
            StringBuilder filePath;

            filePath = new StringBuilder((int)MAX_PATH);
            if (GetProcessImageFileName(processHandle, filePath, MAX_PATH) == 0)
                return null;
            return filePath.ToString();
        }

        /// <summary>
        /// 判断进程是否为64位进程，返回值为方法是否执行成功
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="is64">是否为64位进程</param>
        /// <returns></returns>
        public static bool Is64BitProcess(uint processId, out bool is64)
        {
            SafeNativeHandle processHandle;

            if (!FastWin32Settings.Is64BitOperatingSystem)
            {
                //不是64位系统肯定不会是64位进程
                is64 = false;
                return true;
            }
            using (processHandle = OpenProcessQuery(processId))
                if (processHandle.IsValid)
                    return Is64BitProcessInternal(processHandle, out is64);
                else
                {
                    is64 = false;
                    return false;
                }
        }

        /// <summary>
        /// 判断进程是否为64位进程，返回值为方法是否执行成功
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="is64">是否为64位进程</param>
        /// <returns></returns>
        internal static bool Is64BitProcessInternal(IntPtr processHandle, out bool is64)
        {
            bool isWow64;

            if (!FastWin32Settings.Is64BitOperatingSystem)
            {
                //不是64位系统肯定不会是64位进程
                is64 = false;
                return true;
            }
            if (!IsWow64Process(processHandle, out isWow64))
            {
                //执行失败
                is64 = false;
                return false;
            }
            is64 = !isWow64;
            return true;
        }

        /// <summary>
        /// 暂停进程
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns></returns>
        public static bool SuspendProcess(uint processId)
        {
            SafeNativeHandle processHandle;

            using (processHandle = OpenProcessProcessSuspendResume(processId))
                if (processHandle.IsValid)
                    return SuspendProcessInternal(processHandle);
                else
                    return false;
        }

        /// <summary>
        /// 暂停进程
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <returns></returns>
        internal static bool SuspendProcessInternal(IntPtr processHandle)
        {
            return ZwSuspendProcess(processHandle) != unchecked((uint)-1);
        }

        /// <summary>
        /// 恢复进程
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns></returns>
        public static bool ResumeProcess(uint processId)
        {
            SafeNativeHandle processHandle;

            using (processHandle = OpenProcessProcessSuspendResume(processId))
                if (processHandle.IsValid)
                    return ResumeProcessInternal(processHandle);
                else
                    return false;
        }

        /// <summary>
        /// 恢复进程
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <returns></returns>
        internal static bool ResumeProcessInternal(IntPtr processHandle)
        {
            return ZwResumeProcess(processHandle) != unchecked((uint)-1);
        }

        /// <summary>
        /// 动态提升进程权限，以管理员模式运行当前进程，如果执行成功当前进程将退出，执行失败无反应
        /// </summary>
        /// <param name="windowHandle">主窗口的句柄</param>
        /// <returns></returns>
        public static void SelfElevate(IntPtr windowHandle)
        {
            StringBuilder filePath;
            SHELLEXECUTEINFO shellExecuteInfo;

            filePath = new StringBuilder((int)MAX_PATH);
            if (GetModuleFileName(IntPtr.Zero, filePath, MAX_PATH) == 0)
                return;
            shellExecuteInfo = new SHELLEXECUTEINFO
            {
                cbSize = SHELLEXECUTEINFO.UnmanagedSize,
                hwnd = windowHandle,
                lpVerb = "runas",
                lpFile = filePath.ToString(),
                nShow = 1
            };
            if (ShellExecuteEx(ref shellExecuteInfo))
                Environment.Exit(0);
        }
    }
}

namespace FastWin32.Diagnostics
{
    /// <summary>
    /// 遍历模块回调方法，要继续遍历,返回true;要停止遍历,返回false
    /// </summary>
    /// <param name="moduleHandle">模块句柄</param>
    /// <param name="moduleName">模块名</param>
    /// <param name="filePath">模块文件所在路径</param>
    /// <returns></returns>
    public delegate bool EnumModulesCallback(IntPtr moduleHandle, string moduleName, string filePath);

    /// <summary>
    /// 遍历模块导出函数回调方法，要继续遍历,返回true;要停止遍历,返回false
    /// </summary>
    /// <param name="pFunction">函数指针</param>
    /// <param name="functionName">函数名，当函数以序号方式导出时，此参数为null</param>
    /// <param name="ordinal">函数导出序号</param>
    /// <returns></returns>
    public delegate bool EnumFunctionsCallback(IntPtr pFunction, string functionName, short ordinal);

    /// <summary>
    /// 模块
    /// </summary>
    public static unsafe class Module32
    {
        /// <summary>
        /// 打开进程（内存读+查询）
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns></returns>
        private static SafeNativeHandle OpenProcessVMReadQuery(uint processId)
        {
            return SafeOpenProcess(FastWin32Settings.SeDebugPrivilege ? PROCESS_ALL_ACCESS : PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, processId);
        }

        /// <summary>
        /// 获取当前进程主模块句柄
        /// </summary>
        /// <returns></returns>
        public static IntPtr GetHandle()
        {
            return GetModuleHandle(null);
        }

        /// <summary>
        /// 获取当前进程模块句柄，获取失败时返回 <see cref="IntPtr.Zero"/>
        /// </summary>
        /// <param name="moduleName">模块名</param>
        /// <returns></returns>
        public static IntPtr GetHandle(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
                throw new ArgumentNullException();

            return GetModuleHandle(moduleName);
        }

        /// <summary>
        /// 获取主模块句柄，获取失败时返回 <see cref="IntPtr.Zero"/>
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns></returns>
        public static IntPtr GetHandle(uint processId)
        {
            SafeNativeHandle processHandle;

            using (processHandle = OpenProcessVMReadQuery(processId))
                if (processHandle.IsValid)
                    return GetHandleInternal(processHandle, true, null);
                else
                    return IntPtr.Zero;
        }

        /// <summary>
        /// 获取模块句柄，获取失败时返回 <see cref="IntPtr.Zero"/>
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="moduleName">模块名</param>
        /// <returns></returns>
        public static IntPtr GetHandle(uint processId, string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
                throw new ArgumentNullException();

            SafeNativeHandle processHandle;

            using (processHandle = OpenProcessVMReadQuery(processId))
                if (processHandle.IsValid)
                    return GetHandleInternal(processHandle, false, moduleName);
                else
                    return IntPtr.Zero;
        }

        /// <summary>
        /// 获取模块句柄，获取失败时返回 <see cref="IntPtr.Zero"/>
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="first">是否返回第一个模块句柄</param>
        /// <param name="moduleName">模块名</param>
        /// <returns></returns>
        internal static IntPtr GetHandleInternal(IntPtr processHandle, bool first, string moduleName)
        {
            bool is64;
            bool isXP;
            IntPtr moduleHandle;
            uint size;
            IntPtr[] moduleHandles;
            StringBuilder moduleNameBuffer;

            if (!Process32.Is64BitProcessInternal(processHandle, out is64))
                return IntPtr.Zero;
            isXP = Environment.OSVersion.Version.Major == 5;
            if (isXP)
            {
                //XP兼容
                if (!EnumProcessModules(processHandle, &moduleHandle, (uint)IntPtr.Size, out size))
                    return IntPtr.Zero;
            }
            else
            {
                if (!EnumProcessModulesEx(processHandle, &moduleHandle, (uint)IntPtr.Size, out size, is64 ? LIST_MODULES_64BIT : LIST_MODULES_32BIT))
                    //先获取储存所有模块句柄所需的字节数
                    return IntPtr.Zero;
            }
            if (first)
                //返回第一个模块句柄
                return moduleHandle;
            moduleHandles = new IntPtr[size / IntPtr.Size];
            fixed (IntPtr* p = &moduleHandles[0])
                if (isXP)
                {
                    //XP兼容
                    if (!EnumProcessModules(processHandle, p, size, out size))
                        return IntPtr.Zero;
                }
                else
                {
                    if (!EnumProcessModulesEx(processHandle, p, size, out size, is64 ? LIST_MODULES_64BIT : LIST_MODULES_32BIT))
                        //获取所有模块句柄
                        return IntPtr.Zero;
                }
            moduleNameBuffer = new StringBuilder((int)MAX_MODULE_NAME32);
            for (int i = 0; i < moduleHandles.Length; i++)
            {
                if (!GetModuleBaseName(processHandle, moduleHandles[i], moduleNameBuffer, MAX_MODULE_NAME32))
                    return IntPtr.Zero;
                if (moduleNameBuffer.ToString().Equals(moduleName, StringComparison.OrdinalIgnoreCase))
                    return moduleHandles[i];
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// 遍历模块，遍历成功返回true，失败返回false（返回值与回调方法的返回值无关）
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="callback">回调方法，不能为空</param>
        /// <param name="getModuleName">是否向回调方法提供模块名，默认为是</param>
        /// <param name="getFilePath">是否向回调方法提供模块文件路径，默认为否</param>
        /// <returns></returns>
        public static bool EnumModules(uint processId, EnumModulesCallback callback, bool getModuleName = true, bool getFilePath = false)
        {
            if (callback == null)
                throw new ArgumentNullException();

            SafeNativeHandle processHandle;
            bool is64;
            bool isXP;
            IntPtr moduleHandle;
            uint size;
            IntPtr[] moduleHandles;
            StringBuilder moduleName;
            StringBuilder filePath;

            using (processHandle = OpenProcessVMReadQuery(processId))
                if (processHandle.IsValid)
                {
                    if (!Process32.Is64BitProcessInternal(processHandle, out is64))
                        return false;
                    isXP = Environment.OSVersion.Version.Major == 5;
                    if (isXP)
                    {
                        //XP兼容
                        if (!EnumProcessModules(processHandle, &moduleHandle, (uint)IntPtr.Size, out size))
                            return false;
                    }
                    else
                    {
                        if (!EnumProcessModulesEx(processHandle, &moduleHandle, (uint)IntPtr.Size, out size, is64 ? LIST_MODULES_64BIT : LIST_MODULES_32BIT))
                            //先获取储存所有模块句柄所需的字节数
                            return false;
                    }
                    moduleHandles = new IntPtr[size / IntPtr.Size];
                    fixed (IntPtr* p = &moduleHandles[0])
                        if (isXP)
                        {
                            //XP兼容
                            if (!EnumProcessModules(processHandle, p, size, out size))
                                return false;
                        }
                        else
                        {
                            if (!EnumProcessModulesEx(processHandle, p, size, out size, is64 ? LIST_MODULES_64BIT : LIST_MODULES_32BIT))
                                //获取所有模块句柄
                                return false;
                        }
                    moduleName = getModuleName ? new StringBuilder((int)MAX_MODULE_NAME32) : null;
                    filePath = getFilePath ? new StringBuilder((int)MAX_PATH) : null;
                    for (int i = 0; i < moduleHandles.Length; i++)
                    {
                        if (getModuleName && !GetModuleBaseName(processHandle, moduleHandles[i], moduleName, MAX_MODULE_NAME32))
                            return false;
                        if (getFilePath && GetModuleFileName(processHandle, filePath, MAX_PATH) == 0)
                            return false;
                        if (!callback(moduleHandles[i], getModuleName ? moduleName.ToString() : null, getFilePath ? filePath.ToString() : null))
                            return true;
                    }
                    return true;
                }
                else
                    return false;
        }

        /// <summary>
        /// 获取函数地址
        /// </summary>
        /// <param name="moduleName">模块名</param>
        /// <param name="functionName">函数名</param>
        /// <returns></returns>
        public static IntPtr GetProcAddress(string moduleName, string functionName)
        {
            if (string.IsNullOrEmpty(moduleName))
                throw new ArgumentNullException();
            if (string.IsNullOrEmpty(functionName))
                throw new ArgumentNullException();

            return GetProcAddressInternal(moduleName, functionName);
        }

        /// <summary>
        /// 获取远程进程函数地址
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="moduleName">模块名</param>
        /// <param name="functionName">函数名</param>
        /// <returns></returns>
        public static IntPtr GetProcAddress(uint processId, string moduleName, string functionName)
        {
            if (string.IsNullOrEmpty(moduleName))
                throw new ArgumentNullException();
            if (string.IsNullOrEmpty(functionName))
                throw new ArgumentNullException();

            SafeNativeHandle processHandle;

            using (processHandle = OpenProcessVMReadQuery(processId))
                if (processHandle.IsValid)
                    return GetProcAddressInternal(processHandle, moduleName, functionName);
                else
                    return IntPtr.Zero;
        }

        /// <summary>
        /// 获取函数地址
        /// </summary>
        /// <param name="moduleName">模块名</param>
        /// <param name="functionName">函数名</param>
        /// <returns></returns>
        internal static IntPtr GetProcAddressInternal(string moduleName, string functionName)
        {
            IntPtr moduleHandle;

            moduleHandle = GetModuleHandle(moduleName);
            if (moduleHandle == IntPtr.Zero)
                return IntPtr.Zero;
            return NativeMethods.GetProcAddress(moduleHandle, functionName);
        }

        /// <summary>
        /// 获取远程进程函数地址
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="moduleName">模块名</param>
        /// <param name="functionName">函数名</param>
        /// <returns></returns>
        internal static IntPtr GetProcAddressInternal(IntPtr processHandle, string moduleName, string functionName)
        {
            IntPtr moduleHandle;
            int ntHeaderOffset;
            bool is64;
            int iedRVA;
            IMAGE_EXPORT_DIRECTORY ied;
            int[] nameOffsets;
            string name;
            short ordinal;
            int addressOffset;

            moduleHandle = GetHandleInternal(processHandle, false, moduleName);
            if (moduleHandle == IntPtr.Zero)
                return IntPtr.Zero;
            if (!MemoryIO.ReadInt32Internal(processHandle, moduleHandle + 0x3C, out ntHeaderOffset))
                return IntPtr.Zero;
            if (!Process32.Is64BitProcessInternal(processHandle, out is64))
                return IntPtr.Zero;
            if (is64)
            {
                if (!MemoryIO.ReadInt32Internal(processHandle, moduleHandle + ntHeaderOffset + 0x88, out iedRVA))
                    return IntPtr.Zero;
            }
            else
            {
                if (!MemoryIO.ReadInt32Internal(processHandle, moduleHandle + ntHeaderOffset + 0x78, out iedRVA))
                    return IntPtr.Zero;
            }
            if (!ReadProcessMemory(processHandle, moduleHandle + iedRVA, &ied, (size_t)40, null))
                return IntPtr.Zero;
            nameOffsets = new int[ied.NumberOfNames];
            fixed (void* p = &nameOffsets[0])
                if (!ReadProcessMemory(processHandle, moduleHandle + (int)ied.AddressOfNames, p, (size_t)(ied.NumberOfNames * 4), null))
                    return IntPtr.Zero;
            for (int i = 0; i < ied.NumberOfNames; i++)
            {
                if (!MemoryIO.ReadStringInternal(processHandle, moduleHandle + nameOffsets[i], out name, 40, false, Encoding.ASCII))
                    return IntPtr.Zero;
                if (name == functionName)
                {
                    if (!MemoryIO.ReadInt16Internal(processHandle, moduleHandle + (int)ied.AddressOfNameOrdinals + i * 2, out ordinal))
                        return IntPtr.Zero;
                    if (!MemoryIO.ReadInt32Internal(processHandle, moduleHandle + (int)ied.AddressOfFunctions + ordinal * 4, out addressOffset))
                        return IntPtr.Zero;
                    return moduleHandle + addressOffset;
                }
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// 枚举模块导出函数
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="moduleName">模块名</param>
        /// <param name="callback">回调函数</param>
        /// <returns></returns>
        public static bool EnumFunctions(uint processId, string moduleName, EnumFunctionsCallback callback)
        {
            if (string.IsNullOrEmpty(moduleName))
                throw new ArgumentNullException();
            if (callback == null)
                throw new ArgumentNullException();

            SafeNativeHandle processHandle;

            using (processHandle = OpenProcessVMReadQuery(processId))
                if (processHandle.IsValid)
                    return EnumFunctionsInternal(processHandle, moduleName, callback);
                else
                    return false;
        }

        /// <summary>
        /// 枚举模块导出函数
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="moduleHandle">模块句柄</param>
        /// <param name="callback">回调函数</param>
        /// <returns></returns>
        public static bool EnumFunctions(uint processId, IntPtr moduleHandle, EnumFunctionsCallback callback)
        {
            if (callback == null)
                throw new ArgumentNullException();

            SafeNativeHandle processHandle;

            using (processHandle = OpenProcessVMReadQuery(processId))
                if (processHandle.IsValid)
                    return EnumFunctionsInternal(processHandle, moduleHandle, callback);
                else
                    return false;
        }

        /// <summary>
        /// 枚举模块导出函数
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="moduleName">模块名</param>
        /// <param name="callback">回调函数</param>
        /// <returns></returns>
        internal static bool EnumFunctionsInternal(IntPtr processHandle, string moduleName, EnumFunctionsCallback callback)
        {
            IntPtr moduleHandle;

            moduleHandle = GetHandleInternal(processHandle, false, moduleName);
            if (moduleHandle == IntPtr.Zero)
                return false;
            return EnumFunctionsInternal(processHandle, moduleHandle, callback);
        }

        /// <summary>
        /// 枚举模块导出函数
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="moduleHandle">模块句柄</param>
        /// <param name="callback">回调函数</param>
        /// <returns></returns>
        internal static bool EnumFunctionsInternal(IntPtr processHandle, IntPtr moduleHandle, EnumFunctionsCallback callback)
        {
            int ntHeaderOffset;
            bool is64;
            int iedRVA;
            IMAGE_EXPORT_DIRECTORY ied;
            int[] nameOffsets;
            string functionName;
            short ordinal;
            int addressOffset;

            if (!MemoryIO.ReadInt32Internal(processHandle, moduleHandle + 0x3C, out ntHeaderOffset))
                return false;
            if (!Process32.Is64BitProcessInternal(processHandle, out is64))
                return false;
            if (is64)
            {
                if (!MemoryIO.ReadInt32Internal(processHandle, moduleHandle + ntHeaderOffset + 0x88, out iedRVA))
                    return false;
            }
            else
            {
                if (!MemoryIO.ReadInt32Internal(processHandle, moduleHandle + ntHeaderOffset + 0x78, out iedRVA))
                    return false;
            }
            if (!ReadProcessMemory(processHandle, moduleHandle + iedRVA, &ied, (size_t)40, null))
                return false;
            if (ied.NumberOfNames == 0)
                //无按名称导出函数
                return true;
            nameOffsets = new int[ied.NumberOfNames];
            fixed (void* p = &nameOffsets[0])
                if (!ReadProcessMemory(processHandle, moduleHandle + (int)ied.AddressOfNames, p, (size_t)(ied.NumberOfNames * 4), null))
                    return false;
            for (int i = 0; i < ied.NumberOfNames; i++)
            {
                if (!MemoryIO.ReadStringInternal(processHandle, moduleHandle + nameOffsets[i], out functionName, 40, false, Encoding.ASCII))
                    return false;
                if (!MemoryIO.ReadInt16Internal(processHandle, moduleHandle + ((int)ied.AddressOfNameOrdinals + i * 2), out ordinal))
                    return false;
                if (!MemoryIO.ReadInt32Internal(processHandle, moduleHandle + ((int)ied.AddressOfFunctions + ordinal * 4), out addressOffset))
                    return false;
                if (!callback(moduleHandle + addressOffset, functionName, ordinal))
                    return true;
            }
            return true;
        }
    }
}

namespace FastWin32.Diagnostics
{
    /// <summary>
    /// 注入
    /// </summary>
    public static unsafe class Injector
    {
        #region Constant
        private const int AssemblyPathOffset = 0x200;

        private const int TypeNameOffset = 0x800;

        private const int MethodNameOffset = 0x980;

        private const int ReturnValueOffset = 0xA00;

        private const int CLRVersionOffset = 0xA10;

        private const int CLSID_CLRMetaHostOffset = 0xA60;

        private const int IID_ICLRMetaHostOffset = 0xA70;

        private const int IID_ICLRRuntimeInfoOffset = 0xA80;

        private const int CLSID_CLRRuntimeHostOffset = 0xA90;

        private const int IID_ICLRRuntimeHostOffset = 0xAA0;

        private const int ArgumentOffset = 0xB00;

        private readonly static byte[] CLSID_CLRMetaHost = new Guid(0x9280188D, 0x0E8E, 0x4867, 0xB3, 0x0C, 0x7F, 0xA8, 0x38, 0x84, 0xE8, 0xDE).ToByteArray();

        private readonly static byte[] IID_ICLRMetaHost = new Guid(0xD332DB9E, 0xB9B3, 0x4125, 0x82, 0x07, 0xA1, 0x48, 0x84, 0xF5, 0x32, 0x16).ToByteArray();

        private readonly static byte[] IID_ICLRRuntimeInfo = new Guid(0xBD39D1D2, 0xBA2F, 0x486A, 0x89, 0xB0, 0xB4, 0xB0, 0xCB, 0x46, 0x68, 0x91).ToByteArray();

        private readonly static byte[] CLSID_CLRRuntimeHost = new Guid(0x90F1A06E, 0x7712, 0x4762, 0x86, 0xB5, 0x7A, 0x5E, 0xBA, 0x6B, 0xDB, 0x02).ToByteArray();

        private readonly static byte[] IID_ICLRRuntimeHost = new Guid(0x90F1A06C, 0x7712, 0x4762, 0x86, 0xB5, 0x7A, 0x5E, 0xBA, 0x6B, 0xDB, 0x02).ToByteArray();
        #endregion

        private struct Section
        {
            public uint VirtualSize;

            public uint VirtualAddress;

            public uint SizeOfRawData;

            public uint PointerToRawData;

            public Section(uint virtualSize, uint virtualAddress, uint sizeOfRawData, uint pointerToRawData)
            {
                VirtualSize = virtualSize;
                VirtualAddress = virtualAddress;
                SizeOfRawData = sizeOfRawData;
                PointerToRawData = pointerToRawData;
            }
        }

        /// <summary>
        /// 打开进程（注入使用）
        /// </summary>
        /// <param name="processId">进程句柄</param>
        /// <returns></returns>
        private static SafeNativeHandle OpenProcessInjecting(uint processId)
        {
            return SafeOpenProcess(FastWin32Settings.SeDebugPrivilege ? PROCESS_ALL_ACCESS : PROCESS_CREATE_THREAD | PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_QUERY_INFORMATION, false, processId);
        }

        /// <summary>
        /// 注入非托管DLL
        /// </summary>
        /// <param name="processId">要注入的进程ID</param>
        /// <param name="assemblyPath">要注入程序集的路径</param>
        /// <param name="typeName">类型名（命名空间+类型名，比如NamespaceA.ClassB）</param>
        /// <param name="methodName">方法名（比如MethodC），该方法必须具有此类签名static int MethodName(string)，比如private static int InjectingMain(string argument)</param>
        /// <param name="argument">参数，可传入null。</param>
        /// <returns></returns>
        public static bool InjectManaged(uint processId, string assemblyPath, string typeName, string methodName, string argument)
        {
            if (string.IsNullOrEmpty(assemblyPath))
                throw new ArgumentNullException();
            if (!File.Exists(assemblyPath))
                throw new FileNotFoundException();
            if (string.IsNullOrEmpty(typeName))
                throw new ArgumentNullException();
            if (string.IsNullOrEmpty(methodName))
                throw new ArgumentNullException();

            SafeNativeHandle processHandle;
            int returnValue;

            using (processHandle = OpenProcessInjecting(processId))
                if (processHandle.IsValid)
                    return InjectManagedInternal(processHandle, assemblyPath, typeName, methodName, argument, out returnValue, false);
                else
                    return false;
        }

        /// <summary>
        /// 注入非托管DLL，并获取被调用方法的返回值（警告：被调用方法返回后才能获取到返回值，<see cref="InjectManaged(uint, string, string, string, string, out int)"/>方法将一直等待到被调用方法返回。如果仅注入程序集而不需要获取返回值，请使用重载版本<see cref="InjectManaged(uint, string, string, string, string)"/>）
        /// </summary>
        /// <param name="processId">要注入的进程ID</param>
        /// <param name="assemblyPath">要注入程序集的路径</param>
        /// <param name="typeName">类型名（命名空间+类型名，比如NamespaceA.ClassB）</param>
        /// <param name="methodName">方法名（比如MethodC），该方法必须具有此类签名static int MethodName(string)，比如private static int InjectingMain(string argument)</param>
        /// <param name="argument">参数，可传入null。</param>
        /// <param name="returnValue">被调用方法返回的整数值</param>
        /// <returns></returns>
        public static bool InjectManaged(uint processId, string assemblyPath, string typeName, string methodName, string argument, out int returnValue)
        {
            if (string.IsNullOrEmpty(assemblyPath))
                throw new ArgumentNullException();
            if (!File.Exists(assemblyPath))
                throw new FileNotFoundException();
            if (string.IsNullOrEmpty(typeName))
                throw new ArgumentNullException();
            if (string.IsNullOrEmpty(methodName))
                throw new ArgumentNullException();

            SafeNativeHandle processHandle;

            using (processHandle = OpenProcessInjecting(processId))
                if (processHandle.IsValid)
                    return InjectManagedInternal(processHandle, assemblyPath, typeName, methodName, argument, out returnValue, true);
                else
                {
                    returnValue = 0;
                    return false;
                }
        }

        /// <summary>
        /// 注入非托管DLL
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="assemblyPath">要注入程序集的路径</param>
        /// <param name="typeName">类型名（命名空间+类型名，比如NamespaceA.ClassB）</param>
        /// <param name="methodName">方法名（比如MethodC），该方法必须具有此类签名static int MethodName(string)，比如private static int InjectingMain(string argument)</param>
        /// <param name="argument">参数，可传入null。</param>
        /// <param name="returnValue">被调用方法返回的整数值</param>
        /// <param name="wait">是否等待返回值</param>
        /// <returns></returns>
        internal static bool InjectManagedInternal(IntPtr processHandle, string assemblyPath, string typeName, string methodName, string argument, out int returnValue, bool wait)
        {
            bool isAssembly;
            bool is64;
            string clrVersion;
            IntPtr pFunction;
            IntPtr threadHandle;
            uint exitCode;

            returnValue = 0;
            assemblyPath = Path.GetFullPath(assemblyPath);
            //获取绝对路径
            IsAssembly(assemblyPath, out isAssembly, out clrVersion);
            if (!isAssembly)
                throw new NotSupportedException("将注入的DLL为不是程序集，应该调用InjectUnmanaged方法而非调用InjectManaged方法");
            if (!Process32.Is64BitProcessInternal(processHandle, out is64))
                return false;
            if (!InjectUnmanagedInternal(processHandle, Path.Combine(GetSystemPath(is64), "mscoree.dll")))
                return false;
            //加载对应进程位数的mscoree.dll
            pFunction = WriteAsm(processHandle, clrVersion, assemblyPath, typeName, methodName, argument);
            //获取远程进程中启动CLR的函数指针
            if (pFunction == IntPtr.Zero)
                return false;
            threadHandle = CreateRemoteThread(processHandle, null, size_t.Zero, pFunction, pFunction + ReturnValueOffset, 0, null);
            if (threadHandle == IntPtr.Zero)
                return false;
            if (wait)
            {
                WaitForSingleObject(threadHandle, INFINITE);
                //等待线程结束
                if (!GetExitCodeThread(threadHandle, out exitCode))
                    return false;
                if (!MemoryIO.ReadInt32Internal(processHandle, pFunction + ReturnValueOffset, out returnValue))
                    return false;
                //获取程序集中被调用方法的返回值
                if (!MemoryManagement.FreeMemoryExInternal(processHandle, pFunction))
                    return false;
                return (int)exitCode >= 0;
                //ICLRRuntimeHost::ExecuteInDefaultAppDomain返回S_OK（0）表示成功。HRESULT不能直接比较，大于等于0就是成功
            }
            return true;
        }

        /// <summary>
        /// 注入非托管DLL
        /// </summary>
        /// <param name="processId">要注入的进程ID</param>
        /// <param name="dllPath">要注入DLL的路径</param>
        /// <returns></returns>
        public static bool InjectUnmanaged(uint processId, string dllPath)
        {
            if (string.IsNullOrEmpty(dllPath))
                throw new ArgumentNullException();
            if (!File.Exists(dllPath))
                throw new FileNotFoundException();

            SafeNativeHandle processHandle;
            bool isAssembly;
            string clrVersion;

            using (processHandle = OpenProcessInjecting(processId))
                if (processHandle.IsValid)
                {
                    dllPath = Path.GetFullPath(dllPath);
                    //获取绝对路径
                    IsAssembly(dllPath, out isAssembly, out clrVersion);
                    if (isAssembly)
                        throw new NotSupportedException("将注入的DLL为程序集，应该调用InjectManaged方法而非调用InjectUnmanaged方法");
                    return InjectUnmanagedInternal(processHandle, dllPath);
                    //注入非托管DLL
                }
                else
                    return false;
        }

        /// <summary>
        /// 注入非托管Dll
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="dllPath">要注入的Dll的路径</param>
        /// <returns></returns>
        internal static bool InjectUnmanagedInternal(IntPtr processHandle, string dllPath)
        {
            bool is64;
            IntPtr pLoadLibrary;
            IntPtr pDllPath;
            IntPtr threadHandle;
            uint exitCode;

            if (!Process32.Is64BitProcessInternal(processHandle, out is64))
                return false;
            pLoadLibrary = Module32.GetProcAddressInternal(processHandle, "kernel32.dll", "LoadLibraryW");
            //获取LoadLibrary的函数地址
            pDllPath = MemoryManagement.AllocMemoryExInternal(processHandle, (size_t)(dllPath.Length * 2 + 2), PAGE_EXECUTE_READ);
            try
            {
                if (pDllPath == IntPtr.Zero)
                    return false;
                if (!MemoryIO.WriteStringInternal(processHandle, pDllPath, dllPath))
                    return false;
                threadHandle = CreateRemoteThread(processHandle, null, size_t.Zero, pLoadLibrary, pDllPath, 0, null);
                if (threadHandle == IntPtr.Zero)
                    return false;
                WaitForSingleObject(threadHandle, INFINITE);
                //等待线程结束
                GetExitCodeThread(threadHandle, out exitCode);
                return exitCode != 0;
                //LoadLibrary返回值不为0则调用成功，否则失败
            }
            finally
            {
                MemoryManagement.FreeMemoryExInternal(processHandle, pDllPath);
            }
        }

        /// <summary>
        /// 获取系统文件夹路径
        /// </summary>
        /// <param name="is64">是否64位</param>
        /// <returns></returns>
        private static string GetSystemPath(bool is64)
        {
            return Path.Combine(Environment.GetEnvironmentVariable("SystemRoot"), (!FastWin32Settings.Is64BitOperatingSystem || is64) ? "System32" : "SysWOW64");
        }

        /// <summary>
        /// 写入启动CLR的机器码，返回函数指针
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="clrVersion">CLR版本</param>
        /// <param name="assemblyPath">程序集路径（绝对路径）</param>
        /// <param name="typeName">类型名</param>
        /// <param name="methodName">方法名</param>
        /// <param name="argument">参数（可空，如果非空，长度必须小于2000）</param>
        /// <returns></returns>
        private static IntPtr WriteAsm(IntPtr processHandle, string clrVersion, string assemblyPath, string typeName, string methodName, string argument)
        {
            bool is64;
            byte[] asm;
            IntPtr pFunction;
            IntPtr pCorBindToRuntimeEx;
            IntPtr pCLRCreateInstance;

            if (!Process32.Is64BitProcessInternal(processHandle, out is64))
                return IntPtr.Zero;
            asm = GetAsmCommon(clrVersion, assemblyPath, typeName, methodName, argument);
            pFunction = MemoryManagement.AllocMemoryExInternal(processHandle, (size_t)(0x1000 + (argument == null ? 0 : argument.Length * 2)), PAGE_EXECUTE_READWRITE);
            if (pFunction == IntPtr.Zero)
                return IntPtr.Zero;
            try
            {
                fixed (byte* p = &asm[0])
                {
                    switch (clrVersion)
                    {
                        case "v2.0.50727":
                            pCorBindToRuntimeEx = Module32.GetProcAddressInternal(processHandle, "mscoree.dll", "CorBindToRuntimeEx");
                            if (pCorBindToRuntimeEx == IntPtr.Zero)
                                return IntPtr.Zero;
                            if (is64)
                                SetAsm64V2(p, (long)pFunction, (long)pCorBindToRuntimeEx);
                            else
                                SetAsm32V2(p, (int)pFunction, (int)pCorBindToRuntimeEx);
                            break;
                        case "v4.0.30319":
                            pCLRCreateInstance = Module32.GetProcAddressInternal(processHandle, "mscoree.dll", "CLRCreateInstance");
                            if (pCLRCreateInstance == IntPtr.Zero)
                                return IntPtr.Zero;
                            if (is64)
                                SetAsm64V4(p, (long)pFunction, (long)pCLRCreateInstance);
                            else
                                SetAsm32V4(p, (int)pFunction, (int)pCLRCreateInstance);
                            break;
                        default:
                            return IntPtr.Zero;
                    }
                }
                if (!MemoryIO.WriteBytesInternal(processHandle, pFunction, asm))
                    return IntPtr.Zero;
            }
            catch
            {
                MemoryManagement.FreeMemoryExInternal(processHandle, pFunction);
                return IntPtr.Zero;
            }
            return pFunction;
        }

        /// <summary>
        /// 获取设置好参数的机器码
        /// </summary>
        /// <param name="clrVersion">CLR版本</param>
        /// <param name="assemblyPath">程序集路径（绝对路径）</param>
        /// <param name="typeName">类型名</param>
        /// <param name="methodName">方法名</param>
        /// <param name="argument">参数（可空，如果非空，长度必须小于2000）</param>
        /// <returns></returns>
        private static byte[] GetAsmCommon(string clrVersion, string assemblyPath, string typeName, string methodName, string argument)
        {
            MemoryStream memoryStream;
            byte[] bytes;

            using (memoryStream = new MemoryStream(0x1000 + (argument == null ? 0 : argument.Length * 2)))
            {
                bytes = Encoding.Unicode.GetBytes(assemblyPath);
                memoryStream.Position = AssemblyPathOffset;
                memoryStream.Write(bytes, 0, bytes.Length);
                //assemblyPath
                bytes = Encoding.Unicode.GetBytes(typeName);
                memoryStream.Position = TypeNameOffset;
                memoryStream.Write(bytes, 0, bytes.Length);
                //typeName
                bytes = Encoding.Unicode.GetBytes(methodName);
                memoryStream.Position = MethodNameOffset;
                memoryStream.Write(bytes, 0, bytes.Length);
                //methodName
                bytes = argument == null ? new byte[0] : Encoding.Unicode.GetBytes(argument);
                memoryStream.Position = ArgumentOffset;
                memoryStream.Write(bytes, 0, bytes.Length);
                //argument
                bytes = Encoding.Unicode.GetBytes(clrVersion);
                memoryStream.Position = CLRVersionOffset;
                memoryStream.Write(bytes, 0, bytes.Length);
                //clrVersion
                memoryStream.Position = CLSID_CLRMetaHostOffset;
                memoryStream.Write(CLSID_CLRMetaHost, 0, CLSID_CLRMetaHost.Length);
                memoryStream.Position = IID_ICLRMetaHostOffset;
                memoryStream.Write(IID_ICLRMetaHost, 0, IID_ICLRMetaHost.Length);
                memoryStream.Position = IID_ICLRRuntimeInfoOffset;
                memoryStream.Write(IID_ICLRRuntimeInfo, 0, IID_ICLRRuntimeInfo.Length);
                memoryStream.Position = CLSID_CLRRuntimeHostOffset;
                memoryStream.Write(CLSID_CLRRuntimeHost, 0, CLSID_CLRRuntimeHost.Length);
                memoryStream.Position = IID_ICLRRuntimeHostOffset;
                memoryStream.Write(IID_ICLRRuntimeHost, 0, IID_ICLRRuntimeHost.Length);
                memoryStream.SetLength(memoryStream.Capacity);
                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// 设置启动32位CLR V2的机器码
        /// </summary>
        /// <param name="p">机器码指针</param>
        /// <param name="pFunction">函数指针</param>
        /// <param name="pCorBindToRuntimeEx">CorBindToRuntimeEx的函数指针</param>
        private static void SetAsm32V2(byte* p, int pFunction, int pCorBindToRuntimeEx)
        {
            //HRESULT WINAPI LoadCLR2(DWORD *pReturnValue)
            #region {
            p[0] = 0x55;
            p += 1;
            //push ebp
            p[0] = 0x89;
            p[1] = 0xE5;
            p += 2;
            //mov ebp,esp
            p[0] = 0x83;
            p[1] = 0xEC;
            p[2] = 0x44;
            p += 3;
            //sub esp,byte +0x44
            p[0] = 0x53;
            p += 1;
            //push ebx
            p[0] = 0x56;
            p += 1;
            //push esi
            p[0] = 0x57;
            p += 1;
            //push edi
            p[0] = 0xC7;
            p[1] = 0x45;
            p[2] = 0xFC;
            p[3] = 0x00;
            p[4] = 0x00;
            p[5] = 0x00;
            p[6] = 0x00;
            p += 7;
            #endregion
            #region ICLRRuntimeHost *pRuntimeHost = nullptr;
            //mov dword [ebp-0x4],0x0
            p[0] = 0x8D;
            p[1] = 0x45;
            p[2] = 0xFC;
            p += 3;
            #endregion
            #region CorBindToRuntimeEx(L"v2.0.50727", nullptr, 0, CLSID_CLRRuntimeHost, IID_ICLRRuntimeHost, (LPVOID*)&pRuntimeHost);
            //lea eax,[ebp-0x4]
            p[0] = 0x50;
            p += 1;
            //push eax
            p[0] = 0x68;
            *(int*)(p + 1) = pFunction + IID_ICLRRuntimeHostOffset;
            p += 5;
            //push dword PIID_ICLRRuntimeHost
            p[0] = 0x68;
            *(int*)(p + 1) = pFunction + CLSID_CLRRuntimeHostOffset;
            p += 5;
            //push dword pCLSID_CLRRuntimeHost
            p[0] = 0x6A;
            p[1] = 0x00;
            p += 2;
            //push byte +0x0
            p[0] = 0x6A;
            p[1] = 0x00;
            p += 2;
            //push byte +0x0
            p[0] = 0x68;
            *(int*)(p + 1) = pFunction + CLRVersionOffset;
            p += 5;
            //push dword pCLRVersion
            p[0] = 0xB9;
            *(int*)(p + 1) = pCorBindToRuntimeEx;
            p += 5;
            //mov ecx,pCorBindToRuntimeEx
            p[0] = 0xFF;
            p[1] = 0xD1;
            p += 2;
            //call ecx
            #endregion
            #region pRuntimeHost->Start();
            p[0] = 0x8B;
            p[1] = 0x45;
            p[2] = 0xFC;
            p += 3;
            //mov eax,[ebp-0x4]
            p[0] = 0x8B;
            p[1] = 0x08;
            p += 2;
            //mov ecx,[eax]
            p[0] = 0x8B;
            p[1] = 0x55;
            p[2] = 0xFC;
            p += 3;
            //mov edx,[ebp-0x4]
            p[0] = 0x52;
            p += 1;
            //push edx
            p[0] = 0x8B;
            p[1] = 0x41;
            p[2] = 0x0C;
            p += 3;
            //mov eax,[ecx+0xc]
            p[0] = 0xFF;
            p[1] = 0xD0;
            p += 2;
            //call eax
            #endregion
            #region return pRuntimeHost->ExecuteInDefaultAppDomain(L"assemblyPath", L"typeName", L"methodName", L"argument", pReturnValue);
            p[0] = 0x8B;
            p[1] = 0x45;
            p[2] = 0x08;
            p += 3;
            //mov eax,[ebp+0x8]
            p[0] = 0x50;
            p += 1;
            //push eax
            p[0] = 0x68;
            *(int*)(p + 1) = pFunction + ArgumentOffset;
            p += 5;
            //push dword pArgument
            p[0] = 0x68;
            *(int*)(p + 1) = pFunction + MethodNameOffset;
            p += 5;
            //push dword pMethodName
            p[0] = 0x68;
            *(int*)(p + 1) = pFunction + TypeNameOffset;
            p += 5;
            //push dword pTypeName
            p[0] = 0x68;
            *(int*)(p + 1) = pFunction + AssemblyPathOffset;
            p += 5;
            //push dword pAssemblyPath
            p[0] = 0x8B;
            p[1] = 0x4D;
            p[2] = 0xFC;
            p += 3;
            //mov ecx,[ebp-0x4]
            p[0] = 0x8B;
            p[1] = 0x11;
            p += 2;
            //mov edx,[ecx]
            p[0] = 0x8B;
            p[1] = 0x45;
            p[2] = 0xFC;
            p += 3;
            //mov eax,[ebp-0x4]
            p[0] = 0x50;
            p += 1;
            //push eax
            p[0] = 0x8B;
            p[1] = 0x4A;
            p[2] = 0x2C;
            p += 3;
            //mov ecx,[edx+0x2c]
            p[0] = 0xFF;
            p[1] = 0xD1;
            p += 2;
            //call ecx
            #endregion
            #region }
            p[0] = 0x5F;
            p += 1;
            //pop edi
            p[0] = 0x5E;
            p += 1;
            //pop esi
            p[0] = 0x5B;
            p += 1;
            //pop ebx
            p[0] = 0x89;
            p[1] = 0xEC;
            p += 2;
            //mov esp,ebp
            p[0] = 0x5D;
            p += 1;
            //pop ebp
            p[0] = 0xC2;
            p[1] = 0x04;
            p[2] = 0x00;
            p += 3;
            //ret 0x4
            #endregion
        }

        /// <summary>
        /// 设置启动32位CLR V4的机器码
        /// </summary>
        /// <param name="p">机器码指针</param>
        /// <param name="pFunction">函数指针</param>
        /// <param name="pCLRCreateInstance">CLRCreateInstance的函数指针</param>
        private static void SetAsm32V4(byte* p, int pFunction, int pCLRCreateInstance)
        {
            //HRESULT WINAPI LoadCLR4(DWORD *pReturnValue)
            #region {
            p[0] = 0x55;
            p += 1;
            //push ebp
            p[0] = 0x89;
            p[1] = 0xE5;
            p += 2;
            //mov ebp,esp
            p[0] = 0x83;
            p[1] = 0xEC;
            p[2] = 0x4C;
            p += 3;
            //sub esp,byte +0x4c
            p[0] = 0x53;
            p += 1;
            //push ebx
            p[0] = 0x56;
            p += 1;
            //push esi
            p[0] = 0x57;
            p += 1;
            //push edi
            #endregion
            #region ICLRMetaHost *pMetaHost = nullptr;
            p[0] = 0xC7;
            p[1] = 0x45;
            p[2] = 0xFC;
            p[3] = 0x00;
            p[4] = 0x00;
            p[5] = 0x00;
            p[6] = 0x00;
            p += 7;
            //mov dword [ebp-0x4],0x0
            #endregion
            #region ICLRRuntimeInfo *pRuntimeInfo = nullptr;
            p[0] = 0xC7;
            p[1] = 0x45;
            p[2] = 0xF8;
            p[3] = 0x00;
            p[4] = 0x00;
            p[5] = 0x00;
            p[6] = 0x00;
            p += 7;
            //mov dword [ebp-0x8],0x0
            #endregion
            #region ICLRRuntimeHost *pRuntimeHost = nullptr;
            p[0] = 0xC7;
            p[1] = 0x45;
            p[2] = 0xF4;
            p[3] = 0x00;
            p[4] = 0x00;
            p[5] = 0x00;
            p[6] = 0x00;
            p += 7;
            //mov dword [ebp-0xc],0x0
            #endregion
            #region CLRCreateInstance(CLSID_CLRMetaHost, IID_ICLRMetaHost, (LPVOID*)&pMetaHost);
            p[0] = 0x8D;
            p[1] = 0x45;
            p[2] = 0xFC;
            p += 3;
            //lea eax,[ebp-0x4]
            p[0] = 0x50;
            p += 1;
            //push eax
            p[0] = 0x68;
            *(int*)(p + 1) = pFunction + IID_ICLRMetaHostOffset;
            p += 5;
            //push dword pIID_ICLRMetaHost
            p[0] = 0x68;
            *(int*)(p + 1) = pFunction + CLSID_CLRMetaHostOffset;
            p += 5;
            //push dword pCLSID_CLRMetaHost
            p[0] = 0xB9;
            *(int*)(p + 1) = pCLRCreateInstance;
            p += 5;
            //mov ecx,pCLRCreateInstance
            p[0] = 0xFF;
            p[1] = 0xD1;
            p += 2;
            //call ecx
            #endregion
            #region pMetaHost->GetRuntime(L"v4.0.30319", IID_ICLRRuntimeInfo, (LPVOID*)&pRuntimeInfo);
            p[0] = 0x8D;
            p[1] = 0x45;
            p[2] = 0xF8;
            p += 3;
            //lea eax,[ebp-0x8]
            p[0] = 0x50;
            p += 1;
            //push eax
            p[0] = 0x68;
            *(int*)(p + 1) = pFunction + IID_ICLRRuntimeInfoOffset;
            p += 5;
            //push dword pIID_ICLRRuntimeInfo
            p[0] = 0x68;
            *(int*)(p + 1) = pFunction + CLRVersionOffset;
            p += 5;
            //push dword pCLRVersion
            p[0] = 0x8B;
            p[1] = 0x4D;
            p[2] = 0xFC;
            p += 3;
            //mov ecx,[ebp-0x4]
            p[0] = 0x8B;
            p[1] = 0x11;
            p += 2;
            //mov edx,[ecx]
            p[0] = 0x8B;
            p[1] = 0x45;
            p[2] = 0xFC;
            p += 3;
            //mov eax,[ebp-0x4]
            p[0] = 0x50;
            p += 1;
            //push eax
            p[0] = 0x8B;
            p[1] = 0x4A;
            p[2] = 0x0C;
            p += 3;
            //mov ecx,[edx+0xc]
            p[0] = 0xFF;
            p[1] = 0xD1;
            p += 2;
            //call ecx
            #endregion
            #region pRuntimeInfo->GetInterface(CLSID_CLRRuntimeHost, IID_ICLRRuntimeHost, (LPVOID*)&pRuntimeHost);
            p[0] = 0x8D;
            p[1] = 0x45;
            p[2] = 0xF4;
            p += 3;
            //lea eax,[ebp-0xc]
            p[0] = 0x50;
            p += 1;
            //push eax
            p[0] = 0x68;
            *(int*)(p + 1) = pFunction + IID_ICLRRuntimeHostOffset;
            p += 5;
            //push dword pIID_ICLRRuntimeHost
            p[0] = 0x68;
            *(int*)(p + 1) = pFunction + CLSID_CLRRuntimeHostOffset;
            p += 5;
            //push dword pCLSID_CLRRuntimeHost
            p[0] = 0x8B;
            p[1] = 0x4D;
            p[2] = 0xF8;
            p += 3;
            //mov ecx,[ebp-0x8]
            p[0] = 0x8B;
            p[1] = 0x11;
            p += 2;
            //mov edx,[ecx]
            p[0] = 0x8B;
            p[1] = 0x45;
            p[2] = 0xF8;
            p += 3;
            //mov eax,[ebp-0x8]
            p[0] = 0x50;
            p += 1;
            //push eax
            p[0] = 0x8B;
            p[1] = 0x4A;
            p[2] = 0x24;
            p += 3;
            //mov ecx,[edx+0x24]
            p[0] = 0xFF;
            p[1] = 0xD1;
            p += 2;
            //call ecx
            #endregion
            #region pRuntimeHost->Start();
            p[0] = 0x8B;
            p[1] = 0x45;
            p[2] = 0xF4;
            p += 3;
            //mov eax,[ebp-0xc]
            p[0] = 0x8B;
            p[1] = 0x08;
            p += 2;
            //mov ecx,[eax]
            p[0] = 0x8B;
            p[1] = 0x55;
            p[2] = 0xF4;
            p += 3;
            //mov edx,[ebp-0xc]
            p[0] = 0x52;
            p += 1;
            //push edx
            p[0] = 0x8B;
            p[1] = 0x41;
            p[2] = 0x0C;
            p += 3;
            //mov eax,[ecx+0xc]
            p[0] = 0xFF;
            p[1] = 0xD0;
            p += 2;
            //call eax
            #endregion
            #region return pRuntimeHost->ExecuteInDefaultAppDomain(L"assemblyPath", L"typeName", L"methodName", L"argument", pReturnValue);
            p[0] = 0x8B;
            p[1] = 0x45;
            p[2] = 0x08;
            p += 3;
            //mov eax,[ebp+0x8]
            p[0] = 0x50;
            p += 1;
            //push eax
            p[0] = 0x68;
            *(int*)(p + 1) = pFunction + ArgumentOffset;
            p += 5;
            //push dword pArgument
            p[0] = 0x68;
            *(int*)(p + 1) = pFunction + MethodNameOffset;
            p += 5;
            //push dword pMethodName
            p[0] = 0x68;
            *(int*)(p + 1) = pFunction + TypeNameOffset;
            p += 5;
            //push dword pTypeName
            p[0] = 0x68;
            *(int*)(p + 1) = pFunction + AssemblyPathOffset;
            p += 5;
            //push dword pAssemblyPath
            p[0] = 0x8B;
            p[1] = 0x4D;
            p[2] = 0xF4;
            p += 3;
            //mov ecx,[ebp-0xc]
            p[0] = 0x8B;
            p[1] = 0x11;
            p += 2;
            //mov edx,[ecx]
            p[0] = 0x8B;
            p[1] = 0x45;
            p[2] = 0xF4;
            p += 3;
            //mov eax,[ebp-0xc]
            p[0] = 0x50;
            p += 1;
            //push eax
            p[0] = 0x8B;
            p[1] = 0x4A;
            p[2] = 0x2C;
            p += 3;
            //mov ecx,[edx+0x2c]
            p[0] = 0xFF;
            p[1] = 0xD1;
            p += 2;
            //call ecx
            #endregion
            #region }
            p[0] = 0x5F;
            p += 1;
            //pop edi
            p[0] = 0x5E;
            p += 1;
            //pop esi
            p[0] = 0x5B;
            p += 1;
            //pop ebx
            p[0] = 0x89;
            p[1] = 0xEC;
            p += 2;
            //mov esp,ebp
            p[0] = 0x5D;
            p += 1;
            //pop ebp
            p[0] = 0xC2;
            p[1] = 0x04;
            p[2] = 0x00;
            p += 3;
            //ret 0x4
            #endregion
        }

        /// <summary>
        /// 设置启动64位CLR V2的机器码
        /// </summary>
        /// <param name="p">机器码指针</param>
        /// <param name="pFunction">函数指针</param>
        /// <param name="pCorBindToRuntimeEx">CorBindToRuntimeEx的函数指针</param>
        private static void SetAsm64V2(byte* p, long pFunction, long pCorBindToRuntimeEx)
        {
            //HRESULT WINAPI LoadCLR2(DWORD *pReturnValue)
            #region {
            p[0] = 0x48;
            p[1] = 0x89;
            p[2] = 0x4C;
            p[3] = 0x24;
            p[4] = 0x08;
            p += 5;
            //mov [rsp+0x8],rcx
            p[0] = 0x55;
            p += 1;
            //push rbp
            p[0] = 0x48;
            p[1] = 0x81;
            p[2] = 0xEC;
            p[3] = 0x80;
            p[4] = 0x00;
            p[5] = 0x00;
            p[6] = 0x00;
            p += 7;
            //sub rsp,0x80
            p[0] = 0x48;
            p[1] = 0x8D;
            p[2] = 0x6C;
            p[3] = 0x24;
            p[4] = 0x30;
            p += 5;
            //lea rbp,[rsp+0x30]
            #endregion
            #region ICLRRuntimeHost *pRuntimeHost = nullptr;
            p[0] = 0x48;
            p[1] = 0xC7;
            p[2] = 0x45;
            p[3] = 0x00;
            p[4] = 0x00;
            p[5] = 0x00;
            p[6] = 0x00;
            p[7] = 0x00;
            p += 8;
            //mov qword [rbp+0x0],0x0
            #endregion
            #region CorBindToRuntimeEx(L"v2.0.50727", nullptr, 0, CLSID_CLRRuntimeHost, IID_ICLRRuntimeHost, (LPVOID*)&pRuntimeHost);
            p[0] = 0x48;
            p[1] = 0x8D;
            p[2] = 0x45;
            p[3] = 0x00;
            p += 4;
            //lea rax,[rbp+0x0]
            p[0] = 0x48;
            p[1] = 0x89;
            p[2] = 0x44;
            p[3] = 0x24;
            p[4] = 0x28;
            p += 5;
            //mov [rsp+0x28],rax
            p[0] = 0x48;
            p[1] = 0xB8;
            *(long*)(p + 2) = pFunction + IID_ICLRRuntimeHostOffset;
            p += 10;
            //mov rax,pIID_ICLRRuntimeHost
            p[0] = 0x48;
            p[1] = 0x89;
            p[2] = 0x44;
            p[3] = 0x24;
            p[4] = 0x20;
            p += 5;
            //mov [rsp+0x20],rax
            p[0] = 0x49;
            p[1] = 0xB9;
            *(long*)(p + 2) = pFunction + CLSID_CLRRuntimeHostOffset;
            p += 10;
            //mov r9,pCLSID_CLRRuntimeHost
            p[0] = 0x45;
            p[1] = 0x31;
            p[2] = 0xC0;
            p += 3;
            //xor r8d,r8d
            p[0] = 0x31;
            p[1] = 0xD2;
            p += 2;
            //xor edx,edx
            p[0] = 0x48;
            p[1] = 0xB9;
            *(long*)(p + 2) = pFunction + CLRVersionOffset;
            p += 10;
            //mov rcx,pCLRVersion
            p[0] = 0x49;
            p[1] = 0xBF;
            *(long*)(p + 2) = pCorBindToRuntimeEx;
            p += 10;
            //mov r15,pCorBindToRuntimeEx
            p[0] = 0x41;
            p[1] = 0xFF;
            p[2] = 0xD7;
            p += 3;
            //call r15
            #endregion
            #region pRuntimeHost->Start();
            p[0] = 0x48;
            p[1] = 0x8B;
            p[2] = 0x45;
            p[3] = 0x00;
            p += 4;
            //mov rax,[rbp+0x0]
            p[0] = 0x48;
            p[1] = 0x8B;
            p[2] = 0x00;
            p += 3;
            //mov rax,[rax]
            p[0] = 0x48;
            p[1] = 0x8B;
            p[2] = 0x4D;
            p[3] = 0x00;
            p += 4;
            //mov rcx,[rbp+0x0]
            p[0] = 0xFF;
            p[1] = 0x50;
            p[2] = 0x18;
            p += 3;
            //call [rax+0x18]
            #endregion
            #region return pRuntimeHost->ExecuteInDefaultAppDomain(L"assemblyPath", L"typeName", L"methodName", L"argument", pReturnValue);
            p[0] = 0x48;
            p[1] = 0x8B;
            p[2] = 0x45;
            p[3] = 0x00;
            p += 4;
            //mov rax,[rbp+0x0]
            p[0] = 0x48;
            p[1] = 0x8B;
            p[2] = 0x00;
            p += 3;
            //mov rax,[rax]
            p[0] = 0x48;
            p[1] = 0x8B;
            p[2] = 0x4D;
            p[3] = 0x60;
            p += 4;
            //mov rcx,[rbp+0x60]
            p[0] = 0x48;
            p[1] = 0x89;
            p[2] = 0x4C;
            p[3] = 0x24;
            p[4] = 0x28;
            p += 5;
            //mov [rsp+0x28],rcx
            p[0] = 0x48;
            p[1] = 0xB9;
            *(long*)(p + 2) = pFunction + ArgumentOffset;
            p += 10;
            //mov rcx,pArgument
            p[0] = 0x48;
            p[1] = 0x89;
            p[2] = 0x4C;
            p[3] = 0x24;
            p[4] = 0x20;
            p += 5;
            //mov [rsp+0x20],rcx
            p[0] = 0x49;
            p[1] = 0xB9;
            *(long*)(p + 2) = pFunction + MethodNameOffset;
            p += 10;
            //mov r9,pMethodName
            p[0] = 0x49;
            p[1] = 0xB8;
            *(long*)(p + 2) = pFunction + TypeNameOffset;
            p += 10;
            //mov r8,pTypeName
            p[0] = 0x48;
            p[1] = 0xBA;
            *(long*)(p + 2) = pFunction + AssemblyPathOffset;
            p += 10;
            //mov rdx,pAssemblyPath
            p[0] = 0x48;
            p[1] = 0x8B;
            p[2] = 0x4D;
            p[3] = 0x00;
            p += 4;
            //mov rcx,[rbp+0x0]
            p[0] = 0xFF;
            p[1] = 0x50;
            p[2] = 0x58;
            p += 3;
            //call [rax+0x58]
            #endregion
            #region }
            p[0] = 0x48;
            p[1] = 0x8D;
            p[2] = 0x65;
            p[3] = 0x50;
            p += 4;
            //lea rsp,[rbp+0x50]
            p[0] = 0x5D;
            p += 1;
            //pop rbp
            p[0] = 0xC3;
            p += 1;
            //ret
            #endregion
        }

        /// <summary>
        /// 设置启动64位CLR V4的机器码
        /// </summary>
        /// <param name="p">机器码指针</param>
        /// <param name="pFunction">函数指针</param>
        /// <param name="pCLRCreateInstance">CLRCreateInstance的函数指针</param>
        private static void SetAsm64V4(byte* p, long pFunction, long pCLRCreateInstance)
        {
            //HRESULT WINAPI LoadCLR4(DWORD *pReturnValue)
            #region {
            p[0] = 0x48;
            p[1] = 0x89;
            p[2] = 0x4C;
            p[3] = 0x24;
            p[4] = 0x08;
            p += 5;
            //mov [rsp+0x8],rcx
            p[0] = 0x55;
            p += 1;
            //push rbp
            p[0] = 0x48;
            p[1] = 0x81;
            p[2] = 0xEC;
            p[3] = 0x90;
            p[4] = 0x00;
            p[5] = 0x00;
            p[6] = 0x00;
            p += 7;
            //sub rsp,0x90
            p[0] = 0x48;
            p[1] = 0x8D;
            p[2] = 0x6C;
            p[3] = 0x24;
            p[4] = 0x30;
            p += 5;
            //lea rbp,[rsp+0x30]
            #endregion
            #region ICLRMetaHost *pMetaHost = nullptr;
            p[0] = 0x48;
            p[1] = 0xC7;
            p[2] = 0x45;
            p[3] = 0x00;
            p[4] = 0x00;
            p[5] = 0x00;
            p[6] = 0x00;
            p[7] = 0x00;
            p += 8;
            //mov qword [rbp+0x0],0x0
            #endregion
            #region ICLRRuntimeInfo *pRuntimeInfo = nullptr;
            p[0] = 0x48;
            p[1] = 0xC7;
            p[2] = 0x45;
            p[3] = 0x08;
            p[4] = 0x00;
            p[5] = 0x00;
            p[6] = 0x00;
            p[7] = 0x00;
            p += 8;
            //mov qword [rbp+0x8],0x0
            #endregion
            #region ICLRRuntimeHost *pRuntimeHost = nullptr;
            p[0] = 0x48;
            p[1] = 0xC7;
            p[2] = 0x45;
            p[3] = 0x10;
            p[4] = 0x00;
            p[5] = 0x00;
            p[6] = 0x00;
            p[7] = 0x00;
            p += 8;
            //mov qword [rbp+0x10],0x0
            #endregion
            #region CLRCreateInstance(CLSID_CLRMetaHost, IID_ICLRMetaHost, (LPVOID*)&pMetaHost);
            p[0] = 0x4C;
            p[1] = 0x8D;
            p[2] = 0x45;
            p[3] = 0x00;
            p += 4;
            //lea r8,[rbp+0x0]
            p[0] = 0x48;
            p[1] = 0xBA;
            *(long*)(p + 2) = pFunction + IID_ICLRMetaHostOffset;
            p += 10;
            //mov rdx,pIID_ICLRMetaHost
            p[0] = 0x48;
            p[1] = 0xB9;
            *(long*)(p + 2) = pFunction + CLSID_CLRMetaHostOffset;
            p += 10;
            //mov rcx,pCLSID_CLRMetaHost
            p[0] = 0x49;
            p[1] = 0xBF;
            *(long*)(p + 2) = pCLRCreateInstance;
            p += 10;
            //mov r15,pCLRCreateInstance
            p[0] = 0x41;
            p[1] = 0xFF;
            p[2] = 0xD7;
            p += 3;
            //call r15
            #endregion
            #region pMetaHost->GetRuntime(L"v4.0.30319", IID_ICLRRuntimeInfo, (LPVOID*)&pRuntimeInfo);
            p[0] = 0x48;
            p[1] = 0x8B;
            p[2] = 0x45;
            p[3] = 0x00;
            p += 4;
            //mov rax,[rbp+0x0]
            p[0] = 0x48;
            p[1] = 0x8B;
            p[2] = 0x00;
            p += 3;
            //mov rax,[rax]
            p[0] = 0x4C;
            p[1] = 0x8D;
            p[2] = 0x4D;
            p[3] = 0x08;
            p += 4;
            //lea r9,[rbp+0x8]
            p[0] = 0x49;
            p[1] = 0xB8;
            *(long*)(p + 2) = pFunction + IID_ICLRRuntimeInfoOffset;
            p += 10;
            //mov r8,pIID_ICLRRuntimeInfo
            p[0] = 0x48;
            p[1] = 0xBA;
            *(long*)(p + 2) = pFunction + CLRVersionOffset;
            p += 10;
            //mov rdx,pCLRVersion
            p[0] = 0x48;
            p[1] = 0x8B;
            p[2] = 0x4D;
            p[3] = 0x00;
            p += 4;
            //mov rcx,[rbp+0x0]
            p[0] = 0xFF;
            p[1] = 0x50;
            p[2] = 0x18;
            p += 3;
            //call [rax+0x18]
            #endregion
            #region pRuntimeInfo->GetInterface(CLSID_CLRRuntimeHost, IID_ICLRRuntimeHost, (LPVOID*)&pRuntimeHost);
            p[0] = 0x48;
            p[1] = 0x8B;
            p[2] = 0x45;
            p[3] = 0x08;
            p += 4;
            //mov rax,[rbp+0x8]
            p[0] = 0x48;
            p[1] = 0x8B;
            p[2] = 0x00;
            p += 3;
            //mov rax,[rax]
            p[0] = 0x4C;
            p[1] = 0x8D;
            p[2] = 0x4D;
            p[3] = 0x10;
            p += 4;
            //lea r9,[rbp+0x10]
            p[0] = 0x49;
            p[1] = 0xB8;
            *(long*)(p + 2) = pFunction + IID_ICLRRuntimeHostOffset;
            p += 10;
            //mov r8,pIID_ICLRRuntimeHost
            p[0] = 0x48;
            p[1] = 0xBA;
            *(long*)(p + 2) = pFunction + CLSID_CLRRuntimeHostOffset;
            p += 10;
            //mov rdx,pCLSID_CLRRuntimeHost
            p[0] = 0x48;
            p[1] = 0x8B;
            p[2] = 0x4D;
            p[3] = 0x08;
            p += 4;
            //mov rcx,[rbp+0x8]
            p[0] = 0xFF;
            p[1] = 0x50;
            p[2] = 0x48;
            p += 3;
            //call [rax+0x48]
            #endregion
            #region pRuntimeHost->Start();
            p[0] = 0x48;
            p[1] = 0x8B;
            p[2] = 0x45;
            p[3] = 0x10;
            p += 4;
            //mov rax,[rbp+0x10]
            p[0] = 0x48;
            p[1] = 0x8B;
            p[2] = 0x00;
            p += 3;
            //mov rax,[rax]
            p[0] = 0x48;
            p[1] = 0x8B;
            p[2] = 0x4D;
            p[3] = 0x10;
            p += 4;
            //mov rcx,[rbp+0x10]
            p[0] = 0xFF;
            p[1] = 0x50;
            p[2] = 0x18;
            p += 3;
            //call [rax+0x18]
            #endregion
            #region return pRuntimeHost->ExecuteInDefaultAppDomain(L"assemblyPath", L"typeName", L"methodName", L"argument", pReturnValue);
            p[0] = 0x48;
            p[1] = 0x8B;
            p[2] = 0x45;
            p[3] = 0x10;
            p += 4;
            //mov rax,[rbp+0x10]
            p[0] = 0x48;
            p[1] = 0x8B;
            p[2] = 0x00;
            p += 3;
            //mov rax,[rax]
            p[0] = 0x48;
            p[1] = 0x8B;
            p[2] = 0x4D;
            p[3] = 0x70;
            p += 4;
            //mov rcx,[rbp+0x70]
            p[0] = 0x48;
            p[1] = 0x89;
            p[2] = 0x4C;
            p[3] = 0x24;
            p[4] = 0x28;
            p += 5;
            //mov [rsp+0x28],rcx
            p[0] = 0x48;
            p[1] = 0xB9;
            *(long*)(p + 2) = pFunction + ArgumentOffset;
            p += 10;
            //mov rcx,pArgument
            p[0] = 0x48;
            p[1] = 0x89;
            p[2] = 0x4C;
            p[3] = 0x24;
            p[4] = 0x20;
            p += 5;
            //mov [rsp+0x20],rcx
            p[0] = 0x49;
            p[1] = 0xB9;
            *(long*)(p + 2) = pFunction + MethodNameOffset;
            p += 10;
            //mov r9,pMethodName
            p[0] = 0x49;
            p[1] = 0xB8;
            *(long*)(p + 2) = pFunction + TypeNameOffset;
            p += 10;
            //mov r8,pTypeName
            p[0] = 0x48;
            p[1] = 0xBA;
            *(long*)(p + 2) = pFunction + AssemblyPathOffset;
            p += 10;
            //mov rdx,pAssemblyPath
            p[0] = 0x48;
            p[1] = 0x8B;
            p[2] = 0x4D;
            p[3] = 0x10;
            p += 4;
            //mov rcx,[rbp+0x10]
            p[0] = 0xFF;
            p[1] = 0x50;
            p[2] = 0x58;
            p += 3;
            //call [rax+0x58]
            #endregion
            #region }
            p[0] = 0x48;
            p[1] = 0x8D;
            p[2] = 0x65;
            p[3] = 0x60;
            p += 4;
            //lea rsp,[rbp+0x60]
            p[0] = 0x5D;
            p += 1;
            //pop rbp
            p[0] = 0xC3;
            p += 1;
            //ret
            #endregion
        }

        /// <summary>
        /// 判断是否为程序集，如果是，输出CLR版本
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="isAssembly">是否程序集</param>
        /// <param name="clrVersion">CLR版本</param>
        private static void IsAssembly(string path, out bool isAssembly, out string clrVersion)
        {
            BinaryReader binaryReader;

            try
            {
                using (binaryReader = new BinaryReader(new FileStream(path, FileMode.Open, FileAccess.Read)))
                    clrVersion = GetVersionString(binaryReader);
                isAssembly = true;
            }
            catch
            {
                clrVersion = null;
                isAssembly = false;
            }
        }

        /// <summary>
        /// 获取CLR版本
        /// </summary>
        /// <param name="binaryReader"></param>
        /// <returns></returns>
        private static string GetVersionString(BinaryReader binaryReader)
        {
            uint peOffset;
            bool is64;
            Section[] sections;
            uint rva;
            Section? section;

            GetPEInfo(binaryReader, out peOffset, out is64);
            binaryReader.BaseStream.Position = peOffset + (is64 ? 0xF8 : 0xE8);
            rva = binaryReader.ReadUInt32();
            //.Net MetaData Directory RVA
            if (rva == 0)
                throw new BadImageFormatException("文件不是程序集");
            sections = GetSections(binaryReader);
            section = GetSection(rva, sections);
            if (section == null)
                throw new InvalidDataException("未知格式的二进制文件");
            binaryReader.BaseStream.Position = section.Value.PointerToRawData + rva - section.Value.VirtualAddress + 0x8;
            //.Net MetaData Directory FileOffset
            rva = binaryReader.ReadUInt32();
            //.Net MetaData RVA
            if (rva == 0)
                throw new BadImageFormatException("文件不是程序集");
            section = GetSection(rva, sections);
            if (section == null)
                throw new InvalidDataException("未知格式的二进制文件");
            binaryReader.BaseStream.Position = section.Value.PointerToRawData + rva - section.Value.VirtualAddress + 0xC;
            //.Net MetaData FileOffset
            return Encoding.UTF8.GetString(binaryReader.ReadBytes(binaryReader.ReadInt32() - 2));
        }

        /// <summary>
        /// 获取PE信息
        /// </summary>
        /// <param name="binaryReader"></param>
        /// <param name="peOffset"></param>
        /// <param name="is64"></param>
        private static void GetPEInfo(BinaryReader binaryReader, out uint peOffset, out bool is64)
        {
            ushort machine;

            binaryReader.BaseStream.Position = 0x3C;
            peOffset = binaryReader.ReadUInt32();
            binaryReader.BaseStream.Position = peOffset + 0x4;
            machine = binaryReader.ReadUInt16();
            if (machine != 0x14C && machine != 0x8664)
                throw new InvalidDataException("未知格式的二进制文件");
            is64 = machine == 0x8664;
        }

        /// <summary>
        /// 获取节
        /// </summary>
        /// <param name="binaryReader"></param>
        /// <returns></returns>
        private static Section[] GetSections(BinaryReader binaryReader)
        {
            uint ntHeaderOffset;
            bool is64;
            ushort numberOfSections;
            Section[] sections;

            GetPEInfo(binaryReader, out ntHeaderOffset, out is64);
            numberOfSections = binaryReader.ReadUInt16();
            binaryReader.BaseStream.Position = ntHeaderOffset + (is64 ? 0x108 : 0xF8);
            sections = new Section[numberOfSections];
            for (int i = 0; i < numberOfSections; i++)
            {
                binaryReader.BaseStream.Position += 0x8;
                sections[i] = new Section(binaryReader.ReadUInt32(), binaryReader.ReadUInt32(), binaryReader.ReadUInt32(), binaryReader.ReadUInt32());
                binaryReader.BaseStream.Position += 0x10;
            }
            return sections;
        }

        /// <summary>
        /// 获取RVA对应节
        /// </summary>
        /// <param name="rva"></param>
        /// <param name="sections"></param>
        /// <returns></returns>
        private static Section? GetSection(uint rva, Section[] sections)
        {
            foreach (Section section in sections)
                if (rva >= section.VirtualAddress && rva < section.VirtualAddress + Math.Max(section.VirtualSize, section.SizeOfRawData))
                    return section;
            return null;
        }
    }
}

namespace FastWin32.Memory
{
    /// <summary>
    /// 读取内存页面模式
    /// </summary>
    public enum ReadPageMode
    {
        /// <summary>
        /// 当前地址之后（包括当前地址）
        /// </summary>
        After,

        /// <summary>
        /// 当前地址之前（包括当前地址）
        /// </summary>
        Before,

        /// <summary>
        /// 整个内存页面
        /// </summary>
        Full
    }

    /// <summary>
    /// 遍历页面回调方法，要继续遍历,返回true;要停止遍历,返回false
    /// </summary>
    /// <param name="pageInfo">页面信息</param>
    /// <returns></returns>
    public delegate bool EnumPagesCallback(PageInfo pageInfo);

    /// <summary>
    /// 内存读写
    /// </summary>
    public static unsafe class MemoryIO
    {
        /// <summary>
        /// 打开进程（内存读写+查询）
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns></returns>
        private static SafeNativeHandle OpenProcessVMReadWriteQuery(uint processId)
        {
            return SafeOpenProcess(FastWin32Settings.SeDebugPrivilege ? PROCESS_ALL_ACCESS : PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_QUERY_INFORMATION, false, processId);
        }

        /// <summary>
        /// 获取指针指向的地址
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="p">指针</param>
        /// <returns></returns>
        internal static bool GetPointerAddrInternal(IntPtr processHandle, Pointer p)
        {
            bool is64;
            int newAddr32 = 0;
            long newAddr64 = 0;

            if (!Process32.Is64BitProcessInternal(processHandle, out is64))
                return false;
            if (p._type == PointerType.Address_Offset)
            {
                if (is64)
                {
                    if (!ReadInt64Internal(processHandle, p._baseAddr, out newAddr64))
                        return false;
                    p._lastAddr = (IntPtr)newAddr64;
                }
                else
                {
                    if (!ReadInt32Internal(processHandle, p._baseAddr, out newAddr32))
                        return false;
                    p._lastAddr = (IntPtr)newAddr32;
                }
            }
            else
                p._lastAddr = Module32.GetHandleInternal(processHandle, false, p._moduleName);
            if (p._lastAddr == IntPtr.Zero)
                return false;
            //获取初始地址
            p._lastAddr = (IntPtr)((ulong)p._lastAddr + p._moduleOffset);
            if (p._offset == null)
                return true;
            if (is64)
                for (int i = 0; i < p._offset.Length; i++)
                {
                    if (!ReadInt64Internal(processHandle, p._lastAddr, out newAddr64))
                        return false;
                    p._lastAddr = (IntPtr)(newAddr64 + p._offset[i]);
                }
            else
                for (int i = 0; i < p._offset.Length; i++)
                {
                    if (!ReadInt32Internal(processHandle, p._lastAddr, out newAddr32))
                        return false;
                    p._lastAddr = (IntPtr)(newAddr32 + p._offset[i]);
                }
            return true;
            //计算偏移
        }

        #region 读写模板
        /// <summary>
        /// 内存读写模板回调函数
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <returns></returns>
        private delegate bool IOTemplateCallback(IntPtr processHandle, IntPtr addr);

        /// <summary>
        /// 内存读写模板回调函数
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        private delegate bool IOTemplateCallback<TValue>(IntPtr processHandle, IntPtr addr, out TValue value);

        /// <summary>
        /// 内存读写模板
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="callback">读写器</param>
        /// <returns></returns>
        private static bool IOTemplate(uint processId, IntPtr addr, IOTemplateCallback callback)
        {
            SafeNativeHandle processHandle;
            bool is64;

            using (processHandle = OpenProcessVMReadWriteQuery(processId))
                if (processHandle.IsValid)
                {
                    if (!Process32.Is64BitProcessInternal(processHandle, out is64))
                        return false;
                    if (is64 && !Environment.Is64BitProcess)
                        throw new NotSupportedException("目标进程为64位但当前进程为32位");
                    return callback(processHandle, addr);
                }
                else
                    return false;
        }

        /// <summary>
        /// 内存读写模板
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="callback">读写器</param>
        /// <returns></returns>
        private static bool IOTemplate(uint processId, Pointer p, IOTemplateCallback callback)
        {
            SafeNativeHandle processHandle;
            bool is64;

            using (processHandle = OpenProcessVMReadWriteQuery(processId))
                if (processHandle.IsValid)
                {
                    if (!Process32.Is64BitProcessInternal(processHandle, out is64))
                        return false;
                    if (is64 && !Environment.Is64BitProcess)
                        throw new NotSupportedException("目标进程为64位但当前进程为32位");
                    if (!GetPointerAddrInternal(processHandle, p))
                        return false;
                    return callback(processHandle, p._lastAddr);
                }
                else
                    return false;
        }

        /// <summary>
        /// 内存读取模板
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <param name="callback">读写器</param>
        /// <returns></returns>
        private static bool IOTemplate<TValue>(uint processId, IntPtr addr, out TValue value, IOTemplateCallback<TValue> callback)
        {
            SafeNativeHandle processHandle;
            bool is64;

            using (processHandle = OpenProcessVMReadWriteQuery(processId))
                if (processHandle.IsValid)
                {
                    if (!Process32.Is64BitProcessInternal(processHandle, out is64))
                    {
                        value = default(TValue);
                        return false;
                    }
                    if (is64 && !Environment.Is64BitProcess)
                        throw new NotSupportedException("目标进程为64位但当前进程为32位");
                    return callback(processHandle, addr, out value);
                }
                else
                {
                    value = default(TValue);
                    return false;
                }
        }

        /// <summary>
        /// 内存读写模板
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <param name="callback">读写器</param>
        /// <returns></returns>
        private static bool IOTemplate<TValue>(uint processId, Pointer p, out TValue value, IOTemplateCallback<TValue> callback)
        {
            SafeNativeHandle processHandle;
            bool is64;

            using (processHandle = OpenProcessVMReadWriteQuery(processId))
                if (processHandle.IsValid)
                {
                    if (!Process32.Is64BitProcessInternal(processHandle, out is64))
                    {
                        value = default(TValue);
                        return false;
                    }
                    if (is64 && !Environment.Is64BitProcess)
                        throw new NotSupportedException("目标进程为64位但当前进程为32位");
                    if (!GetPointerAddrInternal(processHandle, p))
                    {
                        value = default(TValue);
                        return false;
                    }
                    return callback(processHandle, p._lastAddr, out value);
                }
                else
                {
                    value = default(TValue);
                    return false;
                }
        }
        #endregion

        #region 读写字节数组
        #region 读取字节数组
        /// <summary>
        /// 读取字节数组，读取的长度由value的长度决定
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadBytes(uint processId, IntPtr addr, byte[] value)
        {
            if (value == null || value.Length == 0)
                throw new ArgumentNullException();

            return IOTemplate(processId, addr, (processHandle, addrCallback) => ReadBytesInternal(processHandle, addrCallback, value));
        }

        /// <summary>
        /// 读取字节数组，读取的长度由value的长度决定
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <param name="numOfRead">实际读取的字节数</param>
        /// <returns></returns>
        public static bool ReadBytes(uint processId, IntPtr addr, byte[] value, out size_t numOfRead)
        {
            if (value == null || value.Length == 0)
                throw new ArgumentNullException();

            return IOTemplate(processId, addr, out numOfRead, (IntPtr processHandle, IntPtr addrCallback, out size_t numOfReadCallback) => ReadBytesInternal(processHandle, addrCallback, value, out numOfReadCallback));
        }

        /// <summary>
        /// 读取字节数组，读取的长度由value的长度决定
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadBytes(uint processId, Pointer p, byte[] value)
        {
            if (value == null || value.Length == 0)
                throw new ArgumentNullException();

            return IOTemplate(processId, p, (processHandle, addrCallback) => ReadBytesInternal(processHandle, addrCallback, value));
        }

        /// <summary>
        /// 读取字节数组，读取的长度由value的长度决定
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <param name="numOfRead">实际读取的字节数</param>
        /// <returns></returns>
        public static bool ReadBytes(uint processId, Pointer p, byte[] value, out size_t numOfRead)
        {
            if (value == null || value.Length == 0)
                throw new ArgumentNullException();

            return IOTemplate(processId, p, out numOfRead, (IntPtr processHandle, IntPtr addrCallback, out size_t numOfReadCallback) => ReadBytesInternal(processHandle, addrCallback, value, out numOfReadCallback));
        }

        /// <summary>
        /// 读取字节数组，读取的长度由value的长度决定
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool ReadBytesInternal(IntPtr processHandle, IntPtr addr, byte[] value) => ReadProcessMemory(processHandle, addr, value, (size_t)value.Length, null);

        /// <summary>
        /// 读取字节数组，读取的长度由value的长度决定
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <param name="numOfRead">实际读取的字节数</param>
        /// <returns></returns>
        internal static bool ReadBytesInternal(IntPtr processHandle, IntPtr addr, byte[] value, out size_t numOfRead) => ReadProcessMemory(processHandle, addr, value, (size_t)value.Length, out numOfRead);
        #endregion

        #region 写入字节数组
        /// <summary>
        /// 写入字节数组，写入的长度由value的长度决定
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteBytes(uint processId, IntPtr addr, byte[] value)
        {
            if (value == null || value.Length == 0)
                throw new ArgumentNullException();

            return IOTemplate(processId, addr, (processHandle, addrCallback) => WriteBytesInternal(processHandle, addrCallback, value));
        }

        /// <summary>
        /// 写入字节数组，写入的长度由value的长度决定
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <param name="numOfWritten">实际写入的字节数</param>
        /// <returns></returns>
        public static bool WriteBytes(uint processId, IntPtr addr, byte[] value, out size_t numOfWritten)
        {
            if (value == null || value.Length == 0)
                throw new ArgumentNullException();

            return IOTemplate(processId, addr, out numOfWritten, (IntPtr processHandle, IntPtr addrCallback, out size_t numOfWrittenCallback) => WriteBytesInternal(processHandle, addrCallback, value, out numOfWrittenCallback));
        }

        /// <summary>
        /// 写入字节数组，写入的长度由value的长度决定
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteBytes(uint processId, Pointer p, byte[] value)
        {
            if (value == null || value.Length == 0)
                throw new ArgumentNullException();

            return IOTemplate(processId, p, (processHandle, addrCallback) => WriteBytesInternal(processHandle, addrCallback, value));
        }

        /// <summary>
        /// 写入字节数组，写入的长度由value的长度决定
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <param name="numOfWritten">实际写入的字节数</param>
        /// <returns></returns>
        public static bool WriteBytes(uint processId, Pointer p, byte[] value, out size_t numOfWritten)
        {
            if (value == null || value.Length == 0)
                throw new ArgumentNullException();

            return IOTemplate(processId, p, out numOfWritten, (IntPtr processHandle, IntPtr addrCallback, out size_t numOfWrittenCallback) => WriteBytesInternal(processHandle, addrCallback, value, out numOfWrittenCallback));
        }

        /// <summary>
        /// 写入字节数组，写入的长度由value的长度决定
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool WriteBytesInternal(IntPtr processHandle, IntPtr addr, byte[] value) => WriteProcessMemory(processHandle, addr, value, (size_t)value.Length, null);

        /// <summary>
        /// 写入字节数组，写入的长度由value的长度决定
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <param name="numOfWritten">实际写入的字节数</param>
        /// <returns></returns>
        internal static bool WriteBytesInternal(IntPtr processHandle, IntPtr addr, byte[] value, out size_t numOfWritten) => WriteProcessMemory(processHandle, addr, value, (size_t)value.Length, out numOfWritten);
        #endregion
        #endregion

        #region 读取页面
        /// <summary>
        /// 读取地址所在内存页面，读取长度由页面大小以及mode决定决定
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <param name="mode">读取模式</param>
        /// <returns></returns>
        public static bool ReadPage(uint processId, IntPtr addr, out byte[] value, ReadPageMode mode) => IOTemplate(processId, addr, out value, (IntPtr processHandle, IntPtr addrCallback, out byte[] buffer) => ReadPageInternal(processHandle, addrCallback, out buffer, mode));

        /// <summary>
        /// 读取地址所在内存页面，读取长度由页面大小以及mode决定决定
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <param name="mode">读取模式</param>
        /// <returns></returns>
        public static bool ReadPage(uint processId, Pointer p, out byte[] value, ReadPageMode mode) => IOTemplate(processId, p, out value, (IntPtr processHandle, IntPtr addrCallback, out byte[] buffer) => ReadPageInternal(processHandle, addrCallback, out buffer, mode));

        /// <summary>
        /// 读取地址所在内存页面，读取长度由页面大小以及mode决定决定
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <param name="mode">读取模式</param>
        /// <returns></returns>
        internal static bool ReadPageInternal(IntPtr processHandle, IntPtr addr, out byte[] value, ReadPageMode mode)
        {
            MEMORY_BASIC_INFORMATION mbi;

            value = null;
            if (VirtualQueryEx(processHandle, addr, out mbi, (size_t)MEMORY_BASIC_INFORMATION.UnmanagedSize) == IntPtr.Zero)
                return false;
            switch (mode)
            {
                case ReadPageMode.After:
                    value = new byte[(int)mbi.BaseAddress + (int)mbi.RegionSize - (int)addr];
                    //读取长度=页面基址+页面大小-当前地址
                    break;
                case ReadPageMode.Before:
                    value = new byte[(int)addr - (int)mbi.BaseAddress + 1];
                    //读取长度=当前地址-页面基址+1
                    break;
                case ReadPageMode.Full:
                    value = new byte[(int)mbi.RegionSize];
                    //读取长度=页面大小
                    addr = mbi.BaseAddress;
                    break;
            }
            return ReadProcessMemory(processHandle, addr, value, (size_t)value.Length, null);
        }
        #endregion

        #region 遍历页面
        /// <summary>
        /// 遍历所有页面
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="callback">回调方法</param>
        /// <returns></returns>
        public static bool EnumPages(uint processId, EnumPagesCallback callback) => IOTemplate(processId, IntPtr.Zero, (processHandle, startAddressCallback) => EnumPagesInternal(processHandle, startAddressCallback, callback));

        /// <summary>
        /// 从指定地址开始遍历页面
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="callback">回调方法</param>
        /// <returns></returns>
        public static bool EnumPages(uint processId, IntPtr startAddress, EnumPagesCallback callback) => IOTemplate(processId, startAddress, (processHandle, startAddressCallback) => EnumPagesInternal(processHandle, startAddressCallback, callback));

        /// <summary>
        /// 从指定地址开始遍历页面
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="callback">回调方法</param>
        /// <returns></returns>
        internal static bool EnumPagesInternal(IntPtr processHandle, IntPtr startAddress, EnumPagesCallback callback)
        {
            bool is64;
            IntPtr nextAddress;
            MEMORY_BASIC_INFORMATION mbi;

            if (!Process32.Is64BitProcessInternal(processHandle, out is64))
                return false;
            if (is64)
            {
                nextAddress = startAddress;
                do
                {
                    if (VirtualQueryEx(processHandle, nextAddress, out mbi, (size_t)MEMORY_BASIC_INFORMATION.UnmanagedSize) == IntPtr.Zero)
                        return Marshal.GetLastWin32Error() == 87;
                    if ((mbi.State & MEM_COMMIT) == MEM_COMMIT && mbi.Protect != 0)
                        if (!callback(new PageInfo(mbi)))
                            return true;
                    nextAddress = (IntPtr)((long)mbi.BaseAddress + (long)mbi.RegionSize);
                } while ((long)nextAddress > 0);
            }
            else
            {
                nextAddress = startAddress;
                if ((ulong)nextAddress > int.MaxValue)
                    return false;
                do
                {
                    if (VirtualQueryEx(processHandle, nextAddress, out mbi, (size_t)MEMORY_BASIC_INFORMATION.UnmanagedSize) == IntPtr.Zero)
                        return false;
                    if ((mbi.State & MEM_COMMIT) == MEM_COMMIT && mbi.Protect != 0)
                        if (!callback(new PageInfo(mbi)))
                            return true;
                    nextAddress = (IntPtr)((int)mbi.BaseAddress + (int)mbi.RegionSize);
                } while ((int)nextAddress > 0);
            }
            return true;
        }
        #endregion

        #region 读写字节
        #region 读取字节
        /// <summary>
        /// 读取字节
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadByte(uint processId, IntPtr addr, out byte value) => IOTemplate(processId, addr, out value, (IntPtr processHandle, IntPtr addrCallback, out byte buffer) => ReadByteInternal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取字节
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadByte(uint processId, Pointer p, out byte value) => IOTemplate(processId, p, out value, (IntPtr processHandle, IntPtr addrCallback, out byte buffer) => ReadByteInternal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取字节
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool ReadByteInternal(IntPtr processHandle, IntPtr addr, out byte value) => ReadProcessMemory(processHandle, addr, out value, (size_t)1, null);
        #endregion

        #region 写入字节
        /// <summary>
        /// 写入字节
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteByte(uint processId, IntPtr addr, byte value) => IOTemplate(processId, addr, (processHandle, addrCallback) => WriteByteInternal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入字节
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteByte(uint processId, Pointer p, byte value) => IOTemplate(processId, p, (processHandle, addrCallback) => WriteByteInternal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入字节
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool WriteByteInternal(IntPtr processHandle, IntPtr addr, byte value) => WriteProcessMemory(processHandle, addr, ref value, (size_t)1, null);
        #endregion
        #endregion

        #region 读写布尔值
        #region 读取布尔值
        /// <summary>
        /// 读取布尔值
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadBoolean(uint processId, IntPtr addr, out bool value) => IOTemplate(processId, addr, out value, (IntPtr processHandle, IntPtr addrCallback, out bool buffer) => ReadBooleanInternal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取布尔值
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadBoolean(uint processId, Pointer p, out bool value) => IOTemplate(processId, p, out value, (IntPtr processHandle, IntPtr addrCallback, out bool buffer) => ReadBooleanInternal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取布尔值
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool ReadBooleanInternal(IntPtr processHandle, IntPtr addr, out bool value) => ReadProcessMemory(processHandle, addr, out value, (size_t)1, null);
        #endregion

        #region 写入布尔值
        /// <summary>
        /// 写入布尔值
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteBoolean(uint processId, IntPtr addr, bool value) => IOTemplate(processId, addr, (processHandle, addrCallback) => WriteBooleanInternal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入布尔值
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteBoolean(uint processId, Pointer p, bool value) => IOTemplate(processId, p, (processHandle, addrCallback) => WriteBooleanInternal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入布尔值
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool WriteBooleanInternal(IntPtr processHandle, IntPtr addr, bool value) => WriteProcessMemory(processHandle, addr, ref value, (size_t)1, null);
        #endregion
        #endregion

        #region 读写字符
        #region 读取字符
        /// <summary>
        /// 读取字符
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadChar(uint processId, IntPtr addr, out char value) => IOTemplate(processId, addr, out value, (IntPtr processHandle, IntPtr addrCallback, out char buffer) => ReadCharInternal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取字符
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadChar(uint processId, Pointer p, out char value) => IOTemplate(processId, p, out value, (IntPtr processHandle, IntPtr addrCallback, out char buffer) => ReadCharInternal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取字符
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool ReadCharInternal(IntPtr processHandle, IntPtr addr, out char value) => ReadProcessMemory(processHandle, addr, out value, (size_t)2, null);
        #endregion

        #region 写入字符
        /// <summary>
        /// 写入字符
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteChar(uint processId, IntPtr addr, char value) => IOTemplate(processId, addr, (processHandle, addrCallback) => WriteCharInternal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入字符
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteChar(uint processId, Pointer p, char value) => IOTemplate(processId, p, (processHandle, addrCallback) => WriteCharInternal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入字符
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool WriteCharInternal(IntPtr processHandle, IntPtr addr, char value) => WriteProcessMemory(processHandle, addr, ref value, (size_t)2, null);
        #endregion
        #endregion

        #region 读写短整形
        #region 读取短整形
        /// <summary>
        /// 读取短整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadInt16(uint processId, IntPtr addr, out short value) => IOTemplate(processId, addr, out value, (IntPtr processHandle, IntPtr addrCallback, out short buffer) => ReadInt16Internal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取短整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadInt16(uint processId, Pointer p, out short value) => IOTemplate(processId, p, out value, (IntPtr processHandle, IntPtr addrCallback, out short buffer) => ReadInt16Internal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取短整形
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool ReadInt16Internal(IntPtr processHandle, IntPtr addr, out short value) => ReadProcessMemory(processHandle, addr, out value, (size_t)2, null);
        #endregion

        #region 写入短整形
        /// <summary>
        /// 写入短整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteInt16(uint processId, IntPtr addr, short value) => IOTemplate(processId, addr, (processHandle, addrCallback) => WriteInt16Internal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入短整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteInt16(uint processId, Pointer p, short value) => IOTemplate(processId, p, (processHandle, addrCallback) => WriteInt16Internal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入短整形
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool WriteInt16Internal(IntPtr processHandle, IntPtr addr, short value) => WriteProcessMemory(processHandle, addr, ref value, (size_t)2, null);
        #endregion
        #endregion

        #region 读写无符号短整形
        #region 读取无符号短整形
        /// <summary>
        /// 读取无符号短整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadUInt16(uint processId, IntPtr addr, out ushort value) => IOTemplate(processId, addr, out value, (IntPtr processHandle, IntPtr addrCallback, out ushort buffer) => ReadUInt16Internal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取无符号短整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadUInt16(uint processId, Pointer p, out ushort value) => IOTemplate(processId, p, out value, (IntPtr processHandle, IntPtr addrCallback, out ushort buffer) => ReadUInt16Internal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取无符号短整形
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool ReadUInt16Internal(IntPtr processHandle, IntPtr addr, out ushort value) => ReadProcessMemory(processHandle, addr, out value, (size_t)2, null);
        #endregion

        #region 写入无符号短整形
        /// <summary>
        /// 写入无符号短整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteUInt16(uint processId, IntPtr addr, ushort value) => IOTemplate(processId, addr, (processHandle, addrCallback) => WriteUInt16Internal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入无符号短整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteUInt16(uint processId, Pointer p, ushort value) => IOTemplate(processId, p, (processHandle, addrCallback) => WriteUInt16Internal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入无符号短整形
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool WriteUInt16Internal(IntPtr processHandle, IntPtr addr, ushort value) => WriteProcessMemory(processHandle, addr, ref value, (size_t)2, null);
        #endregion
        #endregion

        #region 读写整形
        #region 读取整形
        /// <summary>
        /// 读取整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadInt32(uint processId, IntPtr addr, out int value) => IOTemplate(processId, addr, out value, (IntPtr processHandle, IntPtr addrCallback, out int buffer) => ReadInt32Internal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadInt32(uint processId, Pointer p, out int value) => IOTemplate(processId, p, out value, (IntPtr processHandle, IntPtr addrCallback, out int buffer) => ReadInt32Internal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取整形
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool ReadInt32Internal(IntPtr processHandle, IntPtr addr, out int value) => ReadProcessMemory(processHandle, addr, out value, (size_t)4, null);
        #endregion

        #region 写入整形
        /// <summary>
        /// 写入整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteInt32(uint processId, IntPtr addr, int value) => IOTemplate(processId, addr, (processHandle, addrCallback) => WriteInt32Internal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteInt32(uint processId, Pointer p, int value) => IOTemplate(processId, p, (processHandle, addrCallback) => WriteInt32Internal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入整形
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool WriteInt32Internal(IntPtr processHandle, IntPtr addr, int value) => WriteProcessMemory(processHandle, addr, ref value, (size_t)4, null);
        #endregion
        #endregion

        #region 读写无符号整形
        #region 读取无符号整形
        /// <summary>
        /// 读取无符号整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadUInt32(uint processId, IntPtr addr, out uint value) => IOTemplate(processId, addr, out value, (IntPtr processHandle, IntPtr addrCallback, out uint buffer) => ReadUInt32Internal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取无符号整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadUInt32(uint processId, Pointer p, out uint value) => IOTemplate(processId, p, out value, (IntPtr processHandle, IntPtr addrCallback, out uint buffer) => ReadUInt32Internal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取无符号整形
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool ReadUInt32Internal(IntPtr processHandle, IntPtr addr, out uint value) => ReadProcessMemory(processHandle, addr, out value, (size_t)4, null);
        #endregion

        #region 写入无符号整形
        /// <summary>
        /// 写入无符号整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteUInt32(uint processId, IntPtr addr, uint value) => IOTemplate(processId, addr, (processHandle, addrCallback) => WriteUInt32Internal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入无符号整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteUInt32(uint processId, Pointer p, uint value) => IOTemplate(processId, p, (processHandle, addrCallback) => WriteUInt32Internal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入无符号整形
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool WriteUInt32Internal(IntPtr processHandle, IntPtr addr, uint value) => WriteProcessMemory(processHandle, addr, ref value, (size_t)4, null);
        #endregion
        #endregion

        #region 读写长整形
        #region 读取长整形
        /// <summary>
        /// 读取长整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadInt64(uint processId, IntPtr addr, out long value) => IOTemplate(processId, addr, out value, (IntPtr processHandle, IntPtr addrCallback, out long buffer) => ReadInt64Internal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取长整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadInt64(uint processId, Pointer p, out long value) => IOTemplate(processId, p, out value, (IntPtr processHandle, IntPtr addrCallback, out long buffer) => ReadInt64Internal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取长整形
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool ReadInt64Internal(IntPtr processHandle, IntPtr addr, out long value) => ReadProcessMemory(processHandle, addr, out value, (size_t)8, null);
        #endregion

        #region 写入长整形
        /// <summary>
        /// 写入长整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteInt64(uint processId, IntPtr addr, long value) => IOTemplate(processId, addr, (processHandle, addrCallback) => WriteInt64Internal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入长整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteInt64(uint processId, Pointer p, long value) => IOTemplate(processId, p, (processHandle, addrCallback) => WriteInt64Internal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入长整形
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool WriteInt64Internal(IntPtr processHandle, IntPtr addr, long value) => WriteProcessMemory(processHandle, addr, ref value, (size_t)8, null);
        #endregion
        #endregion

        #region 读写无符号长整形
        #region 读取无符号长整形
        /// <summary>
        /// 读取无符号长整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadUInt64(uint processId, IntPtr addr, out ulong value) => IOTemplate(processId, addr, out value, (IntPtr processHandle, IntPtr addrCallback, out ulong buffer) => ReadUInt64Internal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取无符号长整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadUInt64(uint processId, Pointer p, out ulong value) => IOTemplate(processId, p, out value, (IntPtr processHandle, IntPtr addrCallback, out ulong buffer) => ReadUInt64Internal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取无符号长整形
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool ReadUInt64Internal(IntPtr processHandle, IntPtr addr, out ulong value) => ReadProcessMemory(processHandle, addr, out value, (size_t)8, null);
        #endregion

        #region 写入无符号长整形
        /// <summary>
        /// 写入无符号长整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteUInt64(uint processId, IntPtr addr, ulong value) => IOTemplate(processId, addr, (processHandle, addrCallback) => WriteUInt64Internal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入无符号长整形
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteUInt64(uint processId, Pointer p, ulong value) => IOTemplate(processId, p, (processHandle, addrCallback) => WriteUInt64Internal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入无符号长整形
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool WriteUInt64Internal(IntPtr processHandle, IntPtr addr, ulong value) => WriteProcessMemory(processHandle, addr, ref value, (size_t)8, null);
        #endregion
        #endregion

        #region 读写单精度浮点型
        #region 读取单精度浮点型
        /// <summary>
        /// 读取单精度浮点型
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadFloat(uint processId, IntPtr addr, out float value) => IOTemplate(processId, addr, out value, (IntPtr processHandle, IntPtr addrCallback, out float buffer) => ReadFloatInternal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取单精度浮点型
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadFloat(uint processId, Pointer p, out float value) => IOTemplate(processId, p, out value, (IntPtr processHandle, IntPtr addrCallback, out float buffer) => ReadFloatInternal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取单精度浮点型
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool ReadFloatInternal(IntPtr processHandle, IntPtr addr, out float value) => ReadProcessMemory(processHandle, addr, out value, (size_t)4, null);
        #endregion

        #region 写入单精度浮点型
        /// <summary>
        /// 写入单精度浮点型
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteFloat(uint processId, IntPtr addr, float value) => IOTemplate(processId, addr, (processHandle, addrCallback) => WriteFloatInternal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入单精度浮点型
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteFloat(uint processId, Pointer p, float value) => IOTemplate(processId, p, (processHandle, addrCallback) => WriteFloatInternal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入单精度浮点型
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool WriteFloatInternal(IntPtr processHandle, IntPtr addr, float value) => WriteProcessMemory(processHandle, addr, ref value, (size_t)4, null);
        #endregion
        #endregion

        #region 读写双精度浮点型
        #region 读取双精度浮点型
        /// <summary>
        /// 读取双精度浮点型
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadDouble(uint processId, IntPtr addr, out double value) => IOTemplate(processId, addr, out value, (IntPtr processHandle, IntPtr addrCallback, out double buffer) => ReadDoubleInternal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取双精度浮点型
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadDouble(uint processId, Pointer p, out double value) => IOTemplate(processId, p, out value, (IntPtr processHandle, IntPtr addrCallback, out double buffer) => ReadDoubleInternal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取双精度浮点型
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool ReadDoubleInternal(IntPtr processHandle, IntPtr addr, out double value) => ReadProcessMemory(processHandle, addr, out value, (size_t)8, null);
        #endregion

        #region 写入双精度浮点型
        /// <summary>
        /// 写入双精度浮点型
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteDouble(uint processId, IntPtr addr, double value) => IOTemplate(processId, addr, (processHandle, addrCallback) => WriteDoubleInternal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入双精度浮点型
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteDouble(uint processId, Pointer p, double value) => IOTemplate(processId, p, (processHandle, addrCallback) => WriteDoubleInternal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入双精度浮点型
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool WriteDoubleInternal(IntPtr processHandle, IntPtr addr, double value) => WriteProcessMemory(processHandle, addr, ref value, (size_t)8, null);
        #endregion
        #endregion

        #region 读写指针
        #region 读取指针
        /// <summary>
        /// 读取指针
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadIntPtr(uint processId, IntPtr addr, out IntPtr value) => IOTemplate(processId, addr, out value, (IntPtr processHandle, IntPtr addrCallback, out IntPtr buffer) => ReadIntPtrInternal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取指针
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool ReadIntPtr(uint processId, Pointer p, out IntPtr value) => IOTemplate(processId, p, out value, (IntPtr processHandle, IntPtr addrCallback, out IntPtr buffer) => ReadIntPtrInternal(processHandle, addrCallback, out buffer));

        /// <summary>
        /// 读取指针
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool ReadIntPtrInternal(IntPtr processHandle, IntPtr addr, out IntPtr value) => ReadProcessMemory(processHandle, addr, out value, (size_t)IntPtr.Size, null);
        #endregion

        #region 写入指针
        /// <summary>
        /// 写入指针
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteIntPtr(uint processId, IntPtr addr, IntPtr value) => IOTemplate(processId, addr, (processHandle, addrCallback) => WriteIntPtrInternal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入指针
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteIntPtr(uint processId, Pointer p, IntPtr value) => IOTemplate(processId, p, (processHandle, addrCallback) => WriteIntPtrInternal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入指针
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool WriteIntPtrInternal(IntPtr processHandle, IntPtr addr, IntPtr value) => WriteProcessMemory(processHandle, addr, ref value, (size_t)IntPtr.Size, null);
        #endregion
        #endregion

        #region 读写字符串
        #region 读取字符串
        /// <summary>
        /// 读取字符串，使用UTF16编码，如果读取到非托管进程中，并且读取为LPSTR LPWSTTR BSTR等字符串类型，请自行转换为byte[]并使用<see cref="ReadBytes(uint, Pointer, byte[])"/>，<see cref="ReadBytes(uint, IntPtr, byte[])"/>
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <param name="doubleZero">是否以2个\0结尾（比如LPWSTR以2个字节\0结尾，而LPSTR以1个字节\0结尾）</param>
        /// <returns></returns>
        public static bool ReadString(uint processId, IntPtr addr, out string value, bool doubleZero) => IOTemplate(processId, addr, out value, (IntPtr processHandle, IntPtr addrCallback, out string buffer) => ReadStringInternal(processHandle, addrCallback, out buffer, 0x1000, doubleZero, Encoding.Unicode));

        /// <summary>
        /// 读取字符串，如果读取到非托管进程中，并且读取为LPSTR LPWSTTR BSTR等字符串类型，请自行转换为byte[]并使用<see cref="ReadBytes(uint, Pointer, byte[])"/>，<see cref="ReadBytes(uint, IntPtr, byte[])"/>
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <param name="doubleZero">是否以2个\0结尾（比如LPWSTR以2个字节\0结尾，而LPSTR以1个字节\0结尾）</param>
        /// <param name="encoding">编码</param>
        /// <returns></returns>
        public static bool ReadString(uint processId, IntPtr addr, out string value, bool doubleZero, Encoding encoding) => IOTemplate(processId, addr, out value, (IntPtr processHandle, IntPtr addrCallback, out string buffer) => ReadStringInternal(processHandle, addrCallback, out buffer, 0x1000, doubleZero, encoding));

        /// <summary>
        /// 读取字符串，使用UTF16编码，如果读取到非托管进程中，并且读取为LPSTR LPWSTTR BSTR等字符串类型，请自行转换为byte[]并使用<see cref="ReadBytes(uint, Pointer, byte[])"/>，<see cref="ReadBytes(uint, IntPtr, byte[])"/>
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <param name="doubleZero">是否以2个\0结尾（比如LPWSTR以2个字节\0结尾，而LPSTR以1个字节\0结尾）</param>
        /// <returns></returns>
        public static bool ReadString(uint processId, Pointer p, out string value, bool doubleZero) => IOTemplate(processId, p, out value, (IntPtr processHandle, IntPtr addrCallback, out string buffer) => ReadStringInternal(processHandle, addrCallback, out buffer, 0x1000, doubleZero, Encoding.Unicode));

        /// <summary>
        /// 读取字符串，如果读取到非托管进程中，并且读取为LPSTR LPWSTTR BSTR等字符串类型，请自行转换为byte[]并使用<see cref="ReadBytes(uint, Pointer, byte[])"/>，<see cref="ReadBytes(uint, IntPtr, byte[])"/>
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <param name="doubleZero">是否以2个\0结尾（比如LPWSTR以2个字节\0结尾，而LPSTR以1个字节\0结尾）</param>
        /// <param name="encoding">编码</param>
        /// <returns></returns>
        public static bool ReadString(uint processId, Pointer p, out string value, bool doubleZero, Encoding encoding) => IOTemplate(processId, p, out value, (IntPtr processHandle, IntPtr addrCallback, out string buffer) => ReadStringInternal(processHandle, addrCallback, out buffer, 0x1000, doubleZero, encoding));

        /// <summary>
        /// 读取字符串，使用UTF16编码，如果读取到非托管进程中，并且读取为LPSTR LPWSTTR BSTR等字符串类型，请自行转换为byte[]并使用<see cref="ReadBytes(uint, Pointer, byte[])"/>，<see cref="ReadBytes(uint, IntPtr, byte[])"/>
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <param name="bufferSize">缓存大小</param>
        /// <param name="doubleZero">是否以2个\0结尾（比如LPWSTR以2个字节\0结尾，而LPSTR以1个字节\0结尾）</param>
        /// <returns></returns>
        internal static bool ReadStringInternal(IntPtr processHandle, IntPtr addr, out string value, int bufferSize, bool doubleZero) => ReadStringInternal(processHandle, addr, out value, bufferSize, doubleZero, Encoding.Unicode);

        /// <summary>
        /// 读取字符串，如果读取到非托管进程中，并且读取为LPSTR LPWSTTR BSTR等字符串类型，请自行转换为byte[]并使用<see cref="ReadBytes(uint, Pointer, byte[])"/>，<see cref="ReadBytes(uint, IntPtr, byte[])"/>
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <param name="bufferSize">缓存大小</param>
        /// <param name="doubleZero">是否以2个\0结尾（比如LPWSTR以2个字节\0结尾，而LPSTR以1个字节\0结尾）</param>
        /// <param name="encoding">编码</param>
        /// <returns></returns>
        internal static bool ReadStringInternal(IntPtr processHandle, IntPtr addr, out string value, int bufferSize, bool doubleZero, Encoding encoding)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding) + "不能为null");
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize) + "小于等于0");

            byte[] buffer;
            size_t numberOfBytesRead;
            List<byte> bufferList;
            bool lastByteIsZero;

            buffer = null;
            numberOfBytesRead = size_t.Zero;
            bufferList = new List<byte>(bufferSize);
            lastByteIsZero = false;
            for (int i = 0; i < int.MaxValue; i++)
            {
                buffer = new byte[bufferSize];
                ReadProcessMemory(processHandle, addr + bufferSize * i, buffer, (size_t)bufferSize, &numberOfBytesRead);
                //读取到缓存
                if ((int)numberOfBytesRead == bufferSize)
                {
                    //读取完整
                    for (int j = 0; j < bufferSize; j++)
                    {
                        if (buffer[j] == 0)
                        {
                            //出现\0
                            if (doubleZero)
                            {
                                //如果双\0结尾
                                if (lastByteIsZero)
                                    //上一个字节为\0
                                    goto addLastRange;
                                if (j + 1 != bufferSize)
                                {
                                    //不是缓存区最后一个字节
                                    if (buffer[j + 1] == 0)
                                        //下一个字节也为\0
                                        goto addLastRange;
                                }
                                else
                                    //缓存读完，标记上一个字节为\0
                                    lastByteIsZero = true;
                            }
                            else
                                //不是2个\0结尾，直接跳出
                                goto addLastRange;
                        }
                        else
                        {
                            if (lastByteIsZero)
                                //上一个字节为\0，但当前字节不是
                                lastByteIsZero = false;
                        }
                    }
                }
                else if (numberOfBytesRead == size_t.Zero)
                {
                    //读取失败
                    value = null;
                    return false;
                }
                else
                {
                    //读取不完整
                    for (int j = 0; j < (int)numberOfBytesRead; j++)
                    {
                        if (buffer[j] == 0)
                        {
                            //出现\0
                            if (doubleZero)
                            {
                                //如果双\0结尾
                                if (lastByteIsZero)
                                    //上一个字节为\0
                                    goto addLastRange;
                                if (j + 1 != (int)numberOfBytesRead && buffer[j + 1] == 0)
                                    //不是缓存区最后一个字节且下一个字节也为\0
                                    goto addLastRange;
                            }
                            else
                                //不是2个\0结尾，直接跳出
                                goto addLastRange;
                        }
                        else
                        {
                            if (lastByteIsZero)
                                //上一个字节为\0，但当前字节不是
                                lastByteIsZero = false;
                        }
                    }
                }
                bufferList.AddRange(buffer);
            };
            addLastRange:
            numberOfBytesRead -= doubleZero ? 2 : 1;
            for (int i = 0; i < (int)numberOfBytesRead; i++)
                bufferList.Add(buffer[i]);
            if (encoding.CodePage == Encoding.Unicode.CodePage)
                buffer = bufferList.ToArray();
            else
                buffer = Encoding.Convert(encoding, Encoding.Unicode, bufferList.ToArray());
            fixed (void* p = &buffer[0])
                value = new string((char*)p);
            return true;
        }
        #endregion

        #region 写入字符串
        /// <summary>
        /// 写入字符串，使用UTF16编码
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteString(uint processId, IntPtr addr, string value) => IOTemplate(processId, addr, (processHandle, addrCallback) => WriteStringInternal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入字符串
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <param name="encoding">编码</param>
        /// <returns></returns>
        public static bool WriteString(uint processId, IntPtr addr, string value, Encoding encoding) => IOTemplate(processId, addr, (processHandle, addrCallback) => WriteStringInternal(processHandle, addrCallback, value, encoding));

        /// <summary>
        /// 写入字符串，使用UTF16编码
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool WriteString(uint processId, Pointer p, string value) => IOTemplate(processId, p, (processHandle, addrCallback) => WriteStringInternal(processHandle, addrCallback, value));

        /// <summary>
        /// 写入字符串
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="p">指针</param>
        /// <param name="value">值</param>
        /// <param name="encoding">编码</param>
        /// <returns></returns>
        public static bool WriteString(uint processId, Pointer p, string value, Encoding encoding) => IOTemplate(processId, p, (processHandle, addrCallback) => WriteStringInternal(processHandle, addrCallback, value, encoding));

        /// <summary>
        /// 写入字符串，使用UTF16编码
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        internal static bool WriteStringInternal(IntPtr processHandle, IntPtr addr, string value)
        {
            if (value == null)
                throw new ArgumentNullException();

            value += "\0";
            return WriteProcessMemory(processHandle, addr, value, (size_t)(value.Length * 2), null);
        }

        /// <summary>
        /// 写入字符串
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">地址</param>
        /// <param name="value">值</param>
        /// <param name="encoding">编码</param>
        /// <returns></returns>
        internal static bool WriteStringInternal(IntPtr processHandle, IntPtr addr, string value, Encoding encoding)
        {
            if (value == null)
                throw new ArgumentNullException();
            if (encoding == null)
                throw new ArgumentNullException();

            byte[] buffer;

            value += "\0";
            if (encoding.CodePage == Encoding.Unicode.CodePage)
                return WriteProcessMemory(processHandle, addr, value, (size_t)(value.Length * 2), null);
            buffer = Encoding.Convert(Encoding.Unicode, encoding, Encoding.Unicode.GetBytes(value));
            return WriteProcessMemory(processHandle, addr, buffer, (size_t)buffer.Length, null);
        }
        #endregion
        #endregion
    }
}

namespace FastWin32.Memory
{
    /// <summary>
    /// 内存管理
    /// </summary>
    public static class MemoryManagement
    {
        /// <summary>
        /// 打开进程（内存操作）
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns></returns>
        private static SafeNativeHandle OpenProcessVMOperation(uint processId)
        {
            return SafeOpenProcess(FastWin32Settings.SeDebugPrivilege ? PROCESS_ALL_ACCESS : PROCESS_VM_OPERATION, false, processId);
        }

        #region ProtectionFlagsGenerator
        /// <summary>
        /// 所有内存保护选项
        /// </summary>
        private const uint AllMemoryProtectionFlags =
            PAGE_EXECUTE_READ |
            PAGE_EXECUTE_READWRITE |
            PAGE_READONLY |
            PAGE_READWRITE;

        /// <summary>
        /// 根据提供选项生成对应的内存保护标识
        /// </summary>
        /// <param name="writable">可写</param>
        /// <param name="executable">可执行</param>
        /// <returns></returns>
        private static uint ProtectionFlagsGenerator(bool writable, bool executable)
        {
            uint writableFlags;
            uint executableFlags;

            writableFlags =
                PAGE_EXECUTE_READWRITE |
                PAGE_READWRITE;
            //可写
            if (!writable)
                //如果不可写
                writableFlags ^= AllMemoryProtectionFlags;
            executableFlags =
                PAGE_EXECUTE_READ |
                PAGE_EXECUTE_READWRITE;
            //可执行
            if (!executable)
                //如果不可执行
                executableFlags ^= AllMemoryProtectionFlags;
            return writableFlags & executableFlags;
        }
        #endregion

        #region AllocMemory
        /// <summary>
        /// 在当前进程中分配内存（默认可写，不可执行）
        /// </summary>
        /// <param name="size">要分配内存的大小</param>
        /// <returns>分配得到的内存所在地址</returns>
        public static IntPtr AllocMemory(size_t size)
        {
            return AllocMemory(size, true, false);
        }

        /// <summary>
        /// 在当前进程中分配内存
        /// </summary>
        /// <param name="size">要分配内存的大小</param>
        /// <param name="writable">可写</param>
        /// <param name="executable">可执行</param>
        /// <returns>分配得到的内存所在地址</returns>
        public static IntPtr AllocMemory(size_t size, bool writable, bool executable)
        {
            return AllocMemoryInternal(size, ProtectionFlagsGenerator(writable, executable));
        }

        /// <summary>
        /// 分配内存（默认可写，不可执行）
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="size">要分配内存的大小</param>
        /// <returns>分配得到的内存所在地址</returns>
        public static IntPtr AllocMemoryEx(uint processId, size_t size)
        {
            return AllocMemoryEx(processId, size, true, false);
        }

        /// <summary>
        /// 分配内存
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="size">要分配内存的大小</param>
        /// <param name="writable">可写</param>
        /// <param name="executable">可执行</param>
        /// <returns>分配得到的内存所在地址</returns>
        public static IntPtr AllocMemoryEx(uint processId, size_t size, bool writable, bool executable)
        {
            SafeNativeHandle processHandle;

            using (processHandle = OpenProcessVMOperation(processId))
                if (processHandle.IsValid)
                    return AllocMemoryExInternal(processHandle, size, ProtectionFlagsGenerator(writable, executable));
                else
                    return IntPtr.Zero;
        }

        /// <summary>
        /// 在当前进程中分配内存（默认可写，不可执行）
        /// </summary>
        /// <param name="size">要分配内存的大小</param>
        /// <returns>分配得到的内存所在地址</returns>
        internal static IntPtr AllocMemoryInternal(size_t size)
        {
            return VirtualAlloc(IntPtr.Zero, size, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
        }

        /// <summary>
        /// 在当前进程中分配内存
        /// </summary>
        /// <param name="size">要分配内存的大小</param>
        /// <param name="flags">内存保护选项</param>
        /// <returns>分配得到的内存所在地址</returns>
        internal static IntPtr AllocMemoryInternal(size_t size, uint flags)
        {
            return VirtualAlloc(IntPtr.Zero, size, MEM_COMMIT | MEM_RESERVE, flags);
        }

        /// <summary>
        /// 分配内存（默认可写，不可执行）
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="size">要分配内存的大小</param>
        /// <returns>分配得到的内存所在地址</returns>
        internal static IntPtr AllocMemoryExInternal(IntPtr processHandle, size_t size)
        {
            return VirtualAllocEx(processHandle, IntPtr.Zero, size, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
        }

        /// <summary>
        /// 分配内存
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="size">要分配内存的大小</param>
        /// <param name="flags">内存保护选项</param>
        /// <returns>分配得到的内存所在地址</returns>
        internal static IntPtr AllocMemoryExInternal(IntPtr processHandle, size_t size, uint flags)
        {
            return VirtualAllocEx(processHandle, IntPtr.Zero, size, MEM_COMMIT | MEM_RESERVE, flags);
        }
        #endregion

        #region FreeMemory
        /// <summary>
        /// 在当前进程中释放内存（MEM_RELEASE）
        /// </summary>
        /// <param name="addr">指定释放内存的地址</param>
        /// <returns></returns>
        public static bool FreeMemory(IntPtr addr)
        {
            return FreeMemoryInternal(addr);
        }

        /// <summary>
        /// 在当前进程中释放内存（MEM_DECOMMIT）
        /// </summary>
        /// <param name="addr">指定释放内存的地址</param>
        /// <param name="size">要释放内存的大小</param>
        public static bool FreeMemory(IntPtr addr, size_t size)
        {
            return FreeMemoryInternal(addr, size);
        }

        /// <summary>
        /// 释放内存（MEM_RELEASE）
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">指定释放内存的地址</param>
        /// <returns></returns>
        public static bool FreeMemoryEx(uint processId, IntPtr addr)
        {
            SafeNativeHandle processHandle;

            using (processHandle = OpenProcessVMOperation(processId))
                if (processHandle.IsValid)
                    return FreeMemoryInternal(processHandle, addr);
                else
                    return false;
        }

        /// <summary>
        /// 释放内存（MEM_DECOMMIT）
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="addr">指定释放内存的地址</param>
        /// <param name="size">要释放内存的大小</param>
        public static bool FreeMemoryEx(uint processId, IntPtr addr, size_t size)
        {
            SafeNativeHandle processHandle;

            using (processHandle = OpenProcessVMOperation(processId))
                if (processHandle.IsValid)
                    return FreeMemoryExInternal(processHandle, addr, size);
                else
                    return false;
        }

        /// <summary>
        /// 在当前进程中释放内存（MEM_RELEASE）
        /// </summary>
        /// <param name="addr">指定释放内存的地址</param>
        /// <returns></returns>
        internal static bool FreeMemoryInternal(IntPtr addr)
        {
            return VirtualFree(addr, size_t.Zero, MEM_RELEASE);
        }

        /// <summary>
        /// 在当前进程中释放内存（MEM_DECOMMIT）
        /// </summary>
        /// <param name="addr">指定释放内存的地址</param>
        /// <param name="size">要释放内存的大小</param>
        internal static bool FreeMemoryInternal(IntPtr addr, size_t size)
        {
            return VirtualFree(addr, size, MEM_DECOMMIT);
        }

        /// <summary>
        /// 释放内存（MEM_RELEASE）
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">指定释放内存的地址</param>
        /// <returns></returns>
        internal static bool FreeMemoryExInternal(IntPtr processHandle, IntPtr addr)
        {
            return VirtualFreeEx(processHandle, addr, size_t.Zero, MEM_RELEASE);
        }

        /// <summary>
        /// 释放内存（MEM_DECOMMIT）
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="addr">指定释放内存的地址</param>
        /// <param name="size">要释放内存的大小</param>
        internal static bool FreeMemoryExInternal(IntPtr processHandle, IntPtr addr, size_t size)
        {
            return VirtualFreeEx(processHandle, addr, size, MEM_DECOMMIT);
        }
        #endregion
    }
}

namespace FastWin32.Memory
{
    /// <summary>
    /// 指针类型
    /// </summary>
    internal enum PointerType
    {
        /// <summary>
        /// 模块名+偏移
        /// </summary>
        ModuleName_Offset,

        /// <summary>
        /// 地址+偏移
        /// </summary>
        Address_Offset
    }

    /// <summary>
    /// 指针
    /// </summary>
    public sealed class Pointer
    {
        internal string _moduleName;

        internal uint _moduleOffset;

        internal IntPtr _baseAddr;

        internal uint[] _offset;

        internal PointerType _type;

        internal IntPtr _lastAddr;

        /// <summary>
        /// 模块名
        /// </summary>
        public string ModuleName => _type == PointerType.ModuleName_Offset ? _moduleName : throw new NotSupportedException("使用了地址+偏移，未使用模块名");

        /// <summary>
        /// 模块偏移
        /// </summary>
        public uint ModuleOffset => _type == PointerType.ModuleName_Offset ? _moduleOffset : throw new NotSupportedException("使用了地址+偏移，未使用模块偏移");

        /// <summary>
        /// 基础地址
        /// </summary>
        public IntPtr BaseAddr => _type == PointerType.Address_Offset ? _baseAddr : throw new NotSupportedException("使用了模块偏移，未使用地址+偏移");

        /// <summary>
        /// 偏移
        /// </summary>
        public uint[] Offset => _offset;

        /// <summary>
        /// 实例化指针结构
        /// </summary>
        /// <param name="moduleName">模块名</param>
        /// <param name="moduleOffset">模块偏移</param>
        /// <param name="offset">偏移</param>
        public Pointer(string moduleName, uint moduleOffset, params uint[] offset)
        {
            if (string.IsNullOrEmpty(moduleName))
                throw new ArgumentOutOfRangeException();

            _moduleName = moduleName;
            _moduleOffset = moduleOffset;
            _offset = offset;
            _type = PointerType.ModuleName_Offset;
        }

        /// <summary>
        /// 实例化指针结构
        /// </summary>
        /// <param name="baseAddr">基础地址</param>
        /// <param name="offset">偏移</param>
        public Pointer(IntPtr baseAddr, params uint[] offset)
        {
            _baseAddr = baseAddr;
            _offset = offset;
            _type = PointerType.Address_Offset;
        }
    }
}

namespace FastWin32.Memory
{
    /// <summary>
    /// 内存页面信息
    /// </summary>
    public class PageInfo
    {
        /// <summary>
        /// 地址
        /// </summary>
        public IntPtr Address { get; }

        /// <summary>
        /// 大小
        /// </summary>
        public size_t Size { get; }

        /// <summary>
        /// 保护选项
        /// </summary>
        public uint Protect { get; }

        /// <summary>
        /// 页面类型
        /// </summary>
        public uint Type { get; }

        internal PageInfo(MEMORY_BASIC_INFORMATION mbi)
        {
            Address = mbi.BaseAddress;
            Size = mbi.RegionSize;
            Protect = mbi.Protect;
            Type = mbi.Type;
        }

        /// <summary>
        /// 返回表示当前对象的字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            bool is64;

            is64 = (ulong)Address > uint.MaxValue;
            return $"Address=0x{Address.ToString(is64 ? "X16" : "X8")} Size=0x{Size.ToString(is64 ? "X16" : "X8")}";
        }
    }
}


namespace FastWin32.Asm
{
    /// <summary>
    /// Asm编译/反编译错误
    /// </summary>
    [Serializable]
    public sealed class AsmCompilerException : Exception
    {
        /// <summary>
        /// 用指定的错误消息创建新实例
        /// </summary>
        /// <param name="message">描述错误的消息</param>
        internal AsmCompilerException(string message) : base(message)
        {
        }
    }
}


namespace FastWin32.Asm
{
    /// <summary>
    /// 汇编指令，机器码对应表
    /// </summary>
    public sealed class AsmData
    {
        internal List<byte> _byteList;

        /// <summary>
        /// 汇编指令
        /// </summary>
        public string Opcode { get; internal set; }

        /// <summary>
        /// 机器码
        /// </summary>
        public byte[] Bytes { get; internal set; }

        /// <summary>
        /// 实例化对应表
        /// </summary>
        /// <param name="opcode">汇编指令</param>
        internal AsmData(string opcode)
        {
            Opcode = opcode;
            _byteList = new List<byte>();
        }

        /// <summary>
        /// 更新对应的机器码，设置<see cref="_byteList"/>为<see langword="null"/>，转换为只读模式
        /// </summary>
        internal void AsReadOnly()
        {
            Bytes = _byteList.ToArray();
            _byteList = null;
        }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{Opcode} - {BitConverter.ToString(Bytes).Replace("-", string.Empty)}";
        }
    }
}


namespace FastWin32.Asm
{
    /// <summary>
    /// 汇编器，提供编译与反编译支持
    /// </summary>
    public static class Assembler
    {
        /// <summary>
        /// 汇编器路径
        /// </summary>
        private static string _nasmPath;

        /// <summary>
        /// 反汇编器路径
        /// </summary>
        private static string _ndisasmPath;

        /// <summary>
        /// 汇编器路径
        /// </summary>
        public static string NasmPath
        {
            get => _nasmPath;
            set
            {
                if (string.IsNullOrEmpty(value))
                    throw new ArgumentNullException();

                value = Path.GetFullPath(value);
                if (!File.Exists(value))
                    throw new FileNotFoundException();
                _nasmPath = value;
            }
        }

        /// <summary>
        /// 反汇编器路径
        /// </summary>
        public static string NdisasmPath
        {
            get => _ndisasmPath;
            set
            {
                if (string.IsNullOrEmpty(value))
                    throw new ArgumentNullException();

                value = Path.GetFullPath(value);
                if (!File.Exists(value))
                    throw new FileNotFoundException();
                _ndisasmPath = value;
            }
        }

        /// <summary>
        /// 汇编指令转机器码
        /// </summary>
        /// <param name="opcodes">汇编指令</param>
        /// <param name="is64">是否使用64位汇编</param>
        /// <returns></returns>
        public static IList<AsmData> OpcodesToBytes(string[] opcodes, bool is64)
        {
            if (opcodes == null || opcodes.Length == 0)
                throw new ArgumentNullException();
            if (string.IsNullOrEmpty(_nasmPath))
                throw new FileNotFoundException("未设置编译器\"nasm.exe\"的路径");

            string[] output;
            string[] tokens;
            int n;
            List<AsmData> asmDataList;

            output = OpcodesToBytesRaw(opcodes, is64);
            n = 0;
            asmDataList = new List<AsmData>(opcodes.Length);
            foreach (string line in output)
            {
                if (string.IsNullOrEmpty(line))
                    continue;
                tokens = GetTokensNasm(line);
                if (int.Parse(tokens[0]) == n)
                {
                    //汇编指令太长，机器码用了2行或更长
                    for (int i = 0; i < tokens[1].Length; i += 2)
                        asmDataList[n - 1]._byteList.Add(Convert.ToByte(tokens[1].Substring(i, 2), 16));
                    //追加机器码到上一行
                }
                else
                {
                    //下一句汇编指令
                    asmDataList.Add(new AsmData(opcodes[n]));
                    for (int i = 0; i < tokens[1].Length; i += 2)
                        asmDataList[n]._byteList.Add(Convert.ToByte(tokens[1].Substring(i, 2), 16));
                    n++;
                }
            }
            foreach (AsmData asmData in asmDataList)
                asmData.AsReadOnly();
            return asmDataList;
        }

        /// <summary>
        /// 汇编指令转机器码保留原始数据
        /// </summary>
        /// <param name="opcodes">汇编指令</param>
        /// <param name="is64">是否使用64位汇编</param>
        /// <returns></returns>
        private static string[] OpcodesToBytesRaw(string[] opcodes, bool is64)
        {
            string tempFilePathOpcodes;
            string tempFilePathBytes;
            ProcessStartInfo startInfo;
            Process process;
            string error;
            StringBuilder stringBuilder;

            tempFilePathOpcodes = GetTempFileName();
            File.WriteAllLines(tempFilePathOpcodes, opcodes);
            tempFilePathBytes = GetTempFileName();
            startInfo = new ProcessStartInfo
            {
                Arguments = string.Format(" -f win{0} -l {1} {2}", is64 ? "64" : "32", Path.GetFileName(tempFilePathBytes), Path.GetFileName(tempFilePathOpcodes)),
                CreateNoWindow = true,
                FileName = _nasmPath,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetTempPath()
            };
            process = new Process
            {
                StartInfo = startInfo
            };
            process.Start();
            process.WaitForExit();
            error = process.StandardError.ReadLine();
            if (string.IsNullOrEmpty(error))
                return File.ReadAllLines(tempFilePathBytes);
            else
            {
                stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(error);
                while (true)
                {
                    error = process.StandardError.ReadLine();
                    if (string.IsNullOrEmpty(error))
                        break;
                    else
                        stringBuilder.AppendLine(error);
                }
                throw new AsmCompilerException(stringBuilder.ToString());
            }
        }

        /// <summary>
        /// 获取nasm编译后生成list的Token
        /// </summary>
        /// <param name="line">list中的一行</param>
        /// <returns></returns>
        private static string[] GetTokensNasm(string line)
        {
            string[] tokens;

            tokens = new string[2];
            tokens[0] = line.Substring(0, 6).Trim();
            //第n条汇编指令
            tokens[1] = line.Substring(16, 18).Trim();
            //对应的机器码
            return tokens;
        }

        /// <summary>
        /// 获取<see cref="IList{AsmData}"/>中所有机器码（相当于将<see cref="GetAllBytesArray"/>的返回值拼接为一个数组）
        /// </summary>
        /// <param name="asmDataList"><see cref="AsmData"/>列表</param>
        /// <returns></returns>
        public static byte[] GetAllBytes(this IList<AsmData> asmDataList)
        {
            if (asmDataList == null)
                throw new ArgumentNullException();

            return asmDataList.SelectMany(asmData => asmData.Bytes).ToArray();
        }

        /// <summary>
        /// 获取<see cref="IList{AsmData}"/>中所有机器码数组
        /// </summary>
        /// <param name="asmDataList"><see cref="AsmData"/>列表</param>
        /// <returns></returns>
        public static byte[][] GetAllBytesArray(this IList<AsmData> asmDataList)
        {
            if (asmDataList == null)
                throw new ArgumentNullException();

            return asmDataList.Select(asmData => asmData.Bytes).ToArray();
        }

        /// <summary>
        /// 机器码转汇编指令
        /// </summary>
        /// <param name="bytes">机器码</param>
        /// <param name="is64">是否使用64位汇编</param>
        /// <returns></returns>
        public static IList<AsmData> BytesToOpcodes(byte[] bytes, bool is64)
        {
            if (bytes == null || bytes.Length == 0)
                throw new ArgumentNullException();
            if (string.IsNullOrEmpty(_ndisasmPath))
                throw new FileNotFoundException("未设置反编译器\"ndisasm.exe\"的路径");

            string[] output;
            string[] tokens;
            int n;
            List<AsmData> asmDataList;

            output = BytesToOpcodesRaw(bytes, is64);
            n = 0;
            asmDataList = new List<AsmData>(output.Length);
            foreach (string line in output)
            {
                if (string.IsNullOrEmpty(line))
                    continue;
                tokens = GetTokensNdisasm(line);
                if (tokens[1] == null)
                {
                    //汇编指令太长，机器码用了2行或更长
                    for (int i = 0; i < tokens[0].Length; i += 2)
                        asmDataList[n - 1]._byteList.Add(Convert.ToByte(tokens[0].Substring(i, 2), 16));
                    //追加机器码到上一行
                }
                else
                {
                    //下一句汇编指令
                    asmDataList.Add(new AsmData(tokens[1]));
                    for (int i = 0; i < tokens[0].Length; i += 2)
                        asmDataList[n]._byteList.Add(Convert.ToByte(tokens[0].Substring(i, 2), 16));
                    n++;
                }
            }
            foreach (AsmData asmData in asmDataList)
                asmData.AsReadOnly();
            return asmDataList;
        }

        /// <summary>
        /// 机器码转汇编指令保留原始数据
        /// </summary>
        /// <param name="bytes">机器码</param>
        /// <param name="is64">是否使用64位汇编</param>
        /// <returns></returns>
        private static string[] BytesToOpcodesRaw(byte[] bytes, bool is64)
        {
            if (bytes == null || bytes.Length == 0)
                throw new ArgumentNullException();
            if (string.IsNullOrEmpty(_ndisasmPath))
                throw new FileNotFoundException("未设置反编译器\"ndisasm.exe\"的路径");

            string tempFilePathBytes;
            ProcessStartInfo startInfo;
            Process process;
            string line;
            List<string> output;

            tempFilePathBytes = GetTempFileName();
            File.WriteAllBytes(tempFilePathBytes, bytes);
            startInfo = new ProcessStartInfo
            {
                Arguments = string.Format(" -b {0} {1}", is64 ? "64" : "32", Path.GetFileName(tempFilePathBytes)),
                CreateNoWindow = true,
                FileName = _ndisasmPath,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetTempPath()
            };
            process = new Process
            {
                StartInfo = startInfo
            };
            process.Start();
            process.WaitForExit();
            output = new List<string>();
            while (true)
            {
                line = process.StandardOutput.ReadLine();
                if (string.IsNullOrEmpty(line))
                    break;
                else
                    output.Add(line);
            }
            return output.ToArray();
        }

        /// <summary>
        /// 获取ndisasm反编译后生成list的Token
        /// </summary>
        /// <param name="line">list中的一行</param>
        /// <returns></returns>
        private static string[] GetTokensNdisasm(string line)
        {
            string[] tokens;

            tokens = new string[2];
            if (line[9] == '-')
            {
                tokens[0] = line.Substring(10).Trim();
                //机器码
            }
            else
            {
                tokens[0] = line.Substring(10, 16).Trim();
                //机器码
                tokens[1] = line.Substring(28).Trim();
                //汇编指令
            }
            return tokens;
        }

        /// <summary>
        /// 获取<see cref="IList{AsmData}"/>中所有汇编指令
        /// </summary>
        /// <param name="asmDataList"><see cref="AsmData"/>列表</param>
        /// <returns></returns>
        public static string[] GetAllOpcodes(this IList<AsmData> asmDataList)
        {
            if (asmDataList == null)
                throw new ArgumentNullException();

            return asmDataList.Select(asmData => asmData.Opcode).ToArray();
        }

        /// <summary>
        /// 获取临时文件
        /// </summary>
        /// <returns></returns>
        private static string GetTempFileName()
        {
            string path;

            path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".tmp");
            File.WriteAllText(path, null);
            return path;
        }

        /// <summary>
        /// 将机器码写入内存，返回函数指针。如果执行失败，返回<see cref="IntPtr.Zero"/>
        /// </summary>
        /// <param name="bytes">机器码</param>
        /// <returns></returns>
        public static IntPtr GetFunctionPointer(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                throw new ArgumentNullException();

            IntPtr pAsm;

            pAsm = MemoryManagement.AllocMemoryInternal((size_t)bytes.Length, PAGE_EXECUTE_READ);
            //分配内存（可执行）
            if (MemoryIO.WriteBytesInternal(CURRENT_PROCESS, pAsm, bytes))
                return pAsm;
            else
                return IntPtr.Zero;
        }

        /// <summary>
        /// 将汇编指令写入内存，返回函数指针。如果执行失败，返回 <see cref="IntPtr.Zero"/>
        /// </summary>
        /// <param name="opcodes">汇编指令</param>
        /// <param name="is64">是否使用64位汇编</param>
        /// <returns></returns>
        public static IntPtr GetFunctionPointer(string[] opcodes, bool is64)
        {
            if (opcodes == null || opcodes.Length == 0)
                throw new ArgumentNullException();

            return GetFunctionPointer(OpcodesToBytes(opcodes, is64).GetAllBytes());
        }

        /// <summary>
        /// 将机器码写入内存，返回对应的委托。如果执行失败，返回<see langword="null"/>
        /// </summary>
        /// <typeparam name="TDelegate">委托</typeparam>
        /// <param name="bytes">机器码</param>
        /// <returns></returns>
        public static TDelegate GetDelegate<TDelegate>(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                throw new ArgumentNullException();

            IntPtr pFunction;

            pFunction = GetFunctionPointer(bytes);
            if (pFunction == IntPtr.Zero)
                return default(TDelegate);
            else
                return (TDelegate)(object)Marshal.GetDelegateForFunctionPointer(pFunction, typeof(TDelegate));
        }

        /// <summary>
        /// 将机器码写入内存，返回对应的委托。如果执行失败，返回<see langword="null"/>
        /// </summary>
        /// <typeparam name="TDelegate"></typeparam>
        /// <param name="opcodes">汇编指令</param>
        /// <param name="is64">是否使用64位汇编</param>
        /// <returns></returns>
        public static TDelegate GetDelegate<TDelegate>(string[] opcodes, bool is64)
        {
            if (opcodes == null || opcodes.Length == 0)
                throw new ArgumentNullException();

            return (TDelegate)(object)Marshal.GetDelegateForFunctionPointer(GetFunctionPointer(opcodes, is64), typeof(TDelegate));
        }
    }
}


namespace FastWin32.Hook.Method
{
    /// <summary>
    /// 挂钩当前进程API
    /// </summary>
    public sealed class LocalHook
    {
        /// <summary>
        /// 杀死函数，让函数不执行任何动作
        /// </summary>
        /// <param name="moduleName">模块名</param>
        /// <param name="apiName">函数名</param>
        /// <returns></returns>
        public static bool Kill(string moduleName, string apiName)
        {
            if (string.IsNullOrEmpty(moduleName) || string.IsNullOrEmpty(apiName))
                throw new ArgumentNullException();

            return Kill(Module32.GetProcAddressInternal(moduleName, apiName));
        }

        /// <summary>
        /// 杀死方法，让方法不执行任何动作
        /// </summary>
        /// <param name="methodInfo">方法信息</param>
        /// <returns></returns>
        public static bool Kill(MethodInfo methodInfo)
        {
            if (methodInfo == null)
                throw new ArgumentNullException();

            return Kill(methodInfo.MethodHandle.GetFunctionPointer());
        }

        /// <summary>
        /// 杀死函数，让函数不执行任何动作
        /// </summary>
        /// <param name="entry">函数入口地址</param>
        /// <returns></returns>
        public static bool Kill(IntPtr entry)
        {
            return MemoryIO.WriteByteInternal(CURRENT_PROCESS, entry, 0xC3);
        }
    }
}

//using System;
//using System.ComponentModel;
//using System.Reflection;
//using static FastWin32.NativeMethods;

//namespace FastWin32.Hook.Method
//{
//    /// <summary>
//    /// 改变托管/非托管函数的执行过程与结果，若要Hook其他进程，配合Injector类注入Dll使用
//    /// </summary>
//    public class LocalHookOld : IHook,IDisposable
//    {
//        /// <summary>
//        /// 原函数入口地址
//        /// </summary>
//        private IntPtr _origEntry;
//        /// <summary>
//        /// 新函数入口地址
//        /// </summary>
//        private IntPtr _newEntry;
//        /// <summary>
//        /// 是否已经安装
//        /// </summary>
//        private bool _isInstalled;
//        private byte[] _origBytes;
//        private byte[] _newBytes;
//        private bool _isFirst;

//        /// <summary>
//        /// 实例化API钩子（用非托管函数替换非托管函数）
//        /// </summary>
//        /// <param name="origModuleName">原非托管函数所在模块</param>
//        /// <param name="origApiName">原非托管函数名（如果该参数是序数值，则它必须在低位字中; 高阶字必须为零）</param>
//        /// <param name="newModuleName">新非托管函数所在模块</param>
//        /// <param name="newApiName">新非托管函数名（如果该参数是序数值，则它必须在低位字中; 高阶字必须为零）</param>
//        public LocalHookOld(string origModuleName, string origApiName, string newModuleName, string newApiName) : this(GetProcAddressInternal(origModuleName, origApiName), GetProcAddressInternal(newModuleName, newApiName)) { }

//        /// <summary>
//        /// 实例化API钩子（用托管方法替换非托管函数）
//        /// </summary>
//        /// <param name="origModuleName">原非托管函数所在模块</param>
//        /// <param name="origApiName">原非托管函数名（如果该参数是序数值，则它必须在低位字中; 高阶字必须为零）</param>
//        /// <param name="newMethodInfo">新托管方法元数据</param>
//        public LocalHookOld(string origModuleName, string origApiName, MethodInfo newMethodInfo) : this(GetProcAddressInternal(origModuleName, origApiName), newMethodInfo.MethodHandle.GetFunctionPointer()) { }

//        /// <summary>
//        /// 实例化API钩子（用非托管函数替换托管方法）
//        /// </summary>
//        /// <param name="origMethodInfo">原托管方法元数据</param>
//        /// <param name="newModuleName">新非托管函数所在模块</param>
//        /// <param name="newApiName">新非托管函数名（如果该参数是序数值，则它必须在低位字中; 高阶字必须为零）</param>
//        public LocalHookOld(MethodInfo origMethodInfo, string newModuleName, string newApiName) : this(origMethodInfo.MethodHandle.GetFunctionPointer(), GetProcAddressInternal(newModuleName, newApiName)) { }

//        /// <summary>
//        /// 实例化API钩子（用托管方法数替换托管方法）
//        /// </summary>
//        /// <param name="origMethodInfo">原托管方法元数据</param>
//        /// <param name="newMethodInfo">新托管方法元数据</param>
//        public LocalHookOld(MethodInfo origMethodInfo, MethodInfo newMethodInfo) : this(origMethodInfo.MethodHandle.GetFunctionPointer(), newMethodInfo.MethodHandle.GetFunctionPointer()) { }

//        /// <summary>
//        /// 实例化API钩子
//        /// </summary>
//        /// <param name="origEntry">原入口</param>
//        /// <param name="newEntry">新入口</param>
//        public LocalHookOld(IntPtr origEntry, IntPtr newEntry)
//        {
//            if (origEntry == newEntry)
//                throw new ArgumentException("新入口与原入口一致");

//            _origEntry = origEntry;
//            _newEntry = newEntry;
//        }

//        /// <summary>
//        /// 获取函数地址
//        /// </summary>
//        /// <param name="moduleName">模块名</param>
//        /// <param name="procName">函数名</param>
//        /// <returns></returns>
//        internal static IntPtr GetProcAddressInternal(string moduleName, string procName)
//        {
//            if (moduleName == null || procName == null)
//                throw new ArgumentNullException();
//            if (moduleName.Length == 0 || procName.Length == 0)
//                throw new ArgumentOutOfRangeException();

//            IntPtr moduleHandle;
//            IntPtr pFunction;

//            moduleHandle = GetModuleHandle(moduleName);
//            if (moduleHandle == IntPtr.Zero)
//                throw new Win32Exception();
//            pFunction = GetProcAddress(moduleHandle, procName);
//            if (pFunction == IntPtr.Zero)
//                throw new Win32Exception();
//            return pFunction;
//        }

//        /// <summary>
//        /// 安装钩子
//        /// </summary>
//        public bool Install()
//        {
//            _isInstalled = true;
//        }

//        /// <summary>
//        /// 生成跳转需要的字节数组
//        /// </summary>
//        /// <returns></returns>
//        private byte[] GenBytes()
//        {
//            if (Environment.Is64BitProcess)
//            {
//                byte[] bytAddr;

//                bytAddr = BitConverter.GetBytes((long)_origEntry);
//                //获取地址的字节数组形式
//                return new byte[]
//                {
//                    0x48, 0xB8, bytAddr[0], bytAddr[1], bytAddr[2], bytAddr[3], bytAddr[4], bytAddr[5], bytAddr[6], bytAddr[7],
//                    //mov rax, addr
//                    0x50,
//                    //push rax
//                    0xC3
//                    //ret
//                };
//                //64位麻烦一些，因为push imm64不被支持，也就是不能直接push 1234567812345678h
//            }
//            else
//            {
//                byte[] bytAddr;

//                bytAddr = BitConverter.GetBytes((int)_newEntry);
//                //获取地址的字节数组形式
//                return new byte[]
//                {
//                    0x68, bytAddr[0], bytAddr[1], bytAddr[2], bytAddr[3],
//                     //push addr
//                    0xC3
//                     //ret
//                };
//            }
//        }

//        /// <summary>
//        /// 卸载钩子
//        /// </summary>
//        public bool Uninstall()
//        {
//            if (!_isInstalled)
//                throw new NotSupportedException("")
//        }

//        #region IDisposable Support
//        private bool disposedValue = false; // 要检测冗余调用

//        protected virtual void Dispose(bool disposing)
//        {
//            if (!disposedValue)
//            {
//                if (disposing)
//                {
//                    // TODO: 释放托管状态(托管对象)。
//                }

//                // TODO: 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
//                // TODO: 将大型字段设置为 null。

//                disposedValue = true;
//            }
//        }

//        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
//        // ~LocalHook() {
//        //   // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
//        //   Dispose(false);
//        // }

//        // 添加此代码以正确实现可处置模式。
//        public void Dispose()
//        {
//            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
//            Dispose(true);
//            // TODO: 如果在以上内容中替代了终结器，则取消注释以下行。
//            // GC.SuppressFinalize(this);
//        }
//        #endregion
//    }
//}

namespace FastWin32.Hook.Method
{
    /// <summary>
    /// 挂钩远程进程API
    /// </summary>
    public sealed class RemoteHook
    {
    }
}

//using System;
//using System.Reflection;
//using System.Threading;
//using System.Windows.Forms;
//using FastWin32.Diagnostics;
//using static FastWin32.NativeMethods;

//namespace FastWin32.Hook.WindowMessage
//{
//    /// <summary>
//    /// 参数，触发条件与 <see cref="KeyEventHandler"/> 相同，但此事件有返回值。返回 <see langword="false"/> 表示将此次消息继续发送给下一个钩子，返回 <see langword="true"/> 表示屏蔽此次消息，目标窗口将无法收到此次消息
//    /// </summary>
//    /// <param name="sender"></param>
//    /// <param name="e"></param>
//    /// <returns></returns>
//    public delegate bool KeyHookEventHandler(KeyboardHook sender, KeyEventArgs e);

//    /// <summary>
//    /// 参数，触发条件与 <see cref="KeyPressEventHandler"/> 相同，但此事件有返回值。返回 <see langword="false"/> 表示将此次消息继续发送给下一个钩子，返回 <see langword="true"/> 表示屏蔽此次消息，目标窗口将无法收到此次消息
//    /// </summary>
//    /// <param name="sender"></param>
//    /// <param name="e"></param>
//    /// <returns></returns>
//    public delegate bool KeyPressHookEventHandler(KeyboardHook sender, KeyPressEventArgs e);

//    /// <summary>
//    /// 键盘消息钩子
//    /// </summary>
//    public sealed class KeyboardHook
//    {
//        private byte[] _keyboardState = new byte[256];

//        private uint _targetThreadId;

//        private IntPtr _hookHandle;

//        private Thread _hookThread;

//        /// <summary>
//        /// 是否安装
//        /// </summary>
//        public bool IsInstalled { get; private set; }

//        /// <summary>
//        /// 按键按下事件
//        /// </summary>
//        public event KeyHookEventHandler KeyDown;

//        /// <summary>
//        /// 按键弹起事件
//        /// </summary>
//        public event KeyHookEventHandler KeyUp;

//        /// <summary>
//        /// 按键按压事件
//        /// </summary>
//        public event KeyPressHookEventHandler KeyPress;

//        /// <summary>
//        /// 创建全局键盘钩子实例
//        /// </summary>
//        public KeyboardHook()
//        {
//        }

//        /// <summary>
//        /// 对指定线程创建键盘钩子
//        /// </summary>
//        /// <param name="targetThreadId">线程ID</param>
//        public KeyboardHook(uint targetThreadId)
//        {
//            _targetThreadId = targetThreadId;
//        }

//        /// <summary>
//        /// 对指定窗口创建键盘钩子
//        /// </summary>
//        /// <param name="windowHandle">窗口句柄</param>
//        public KeyboardHook(IntPtr windowHandle)
//        {
//            if (!IsWindow(windowHandle))
//                throw new ArgumentNullException("无效窗口句柄");

//            _targetThreadId = GetWindowThreadProcessId(windowHandle, null);
//        }

//        /// <summary>
//        /// 安装钩子
//        /// </summary>
//        public bool Install()
//        {
//            if (IsInstalled)
//                throw new NotSupportedException("无法重复安装钩子");

//            bool finished;
//            bool result;

//            finished = false;
//            result = false;
//            if (Application.MessageLoop)
//            {
//                //调用此方法的线程如果有消息循环，就不需要开新线程启动消息循环
//                result = InstallPrivate();
//                finished = true;
//            }
//            else
//            {
//                _hookThread = new Thread(() =>
//                {
//                    result = InstallPrivate();
//                    finished = true;
//                    Application.Run();
//                })
//                {
//                    IsBackground = true
//                };
//                _hookThread.Start();
//            }
//            while (!finished)
//                Thread.Sleep(0);
//            if (result)
//            {
//                IsInstalled = true;
//                return true;
//            }
//            else
//            {
//                _hookThread?.Abort();
//                return false;
//            }
//        }

//        /// <summary>
//        /// 安装钩子
//        /// </summary>
//        /// <returns></returns>
//        private bool InstallPrivate()
//        {
//            uint processId;
//            string guid;
//            int returnValue;

//            if (_targetThreadId == 0)
//            {
//                _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, LowLevelKeyboardHookProc, IntPtr.Zero, 0);
//                return _hookHandle != IntPtr.Zero;
//            }
//            else
//            {
//                guid = Guid.NewGuid().ToString();
//                //TODO
//                //TODO
//                //TODO
//                //TODO
//                //TODO
//                //TODO
//                //TODO
//                //TODO
//                //TODO
//                //TODO
//                //TODO
//                return (processId = Process32.GetProcessIdByThreadId(_targetThreadId)) != 0 && Injector.InjectManaged(processId, Assembly.GetExecutingAssembly().Location, "FastWin32.Hook.WindowMessage.MessageProxy", "StartServer", guid, out returnValue) && returnValue == 1;
//            }
//        }

//        /// <summary>
//        /// 键盘消息回调函数
//        /// </summary>
//        /// <param name="nCode">挂钩过程用于确定如何处理消息的代码。如果代码小于0，挂钩过程必须将消息传递给CallNextHookEx函数，无需进一步处理，并应返回CallNextHookEx返回的值。</param>
//        /// <param name="wParam">产生击键消息的密钥的虚拟密钥代码。</param>
//        /// <param name="lParam">重复计数，扫描码，扩展密钥标志，上下文代码，先前的密钥状态标志和转换状态标志。有关lParam参数的更多信息，请参阅按键消息标志。下表描述了该值的位。</param>
//        /// <returns></returns>
//        private IntPtr LowLevelKeyboardHookProc(int nCode, size_t wParam, size_t lParam)
//        {
//            if (nCode < 0 || (KeyDown == null && KeyUp == null && KeyPress == null))
//                //如果nCode小于零，则钩子过程必须返回CallNextHookEx返回的值并且不对钩子消息做处理。如果3个事件均未被订阅，直接返回
//                return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
//            else
//            {
//                if (OnKeyEvent((uint)wParam, ((KBDLLHOOKSTRUCT*)lParam)->vkCode, ((KBDLLHOOKSTRUCT*)lParam)->scanCode))
//                    return (IntPtr)(-1);
//                else
//                    return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
//            }
//        }

//        /// <summary>
//        /// 引发事件
//        /// </summary>
//        /// <param name="messageType">消息类型</param>
//        /// <param name="vkCode">虚拟键码</param>
//        /// <param name="scanCode">扫描码</param>
//        /// <returns></returns>
//        private bool OnKeyEvent(uint messageType, uint vkCode, uint scanCode)
//        {
//            bool isBlock;
//            char keyChar;

//            isBlock = false;
//            if (KeyDown != null && (messageType == WM_KEYDOWN || messageType == WM_SYSKEYDOWN))
//                isBlock = KeyDown(this, new KeyEventArgs((Keys)vkCode));
//            if (KeyUp != null && (messageType == WM_KEYUP || messageType == WM_SYSKEYUP))
//                isBlock = KeyUp(this, new KeyEventArgs((Keys)vkCode));
//            if (KeyPress != null && messageType == WM_KEYDOWN)
//            {
//                GetKeyState(0);
//                GetKeyboardState(_keyboardState);
//                if (ToAscii(vkCode, scanCode, _keyboardState, out keyChar, 0) == 1)
//                    isBlock = KeyPress(this, new KeyPressEventArgs(keyChar));
//            }
//            return isBlock;
//        }

//        /// <summary>
//        /// 卸载钩子
//        /// </summary>
//        public bool Uninstall()
//        {
//            if (!IsInstalled)
//                throw new NotSupportedException("未安装钩子");

//            if (_targetThreadId == 0)
//            {
//                //全局钩子
//                if (!UnhookWindowsHookEx(_hookHandle))
//                    //钩子卸载失败
//                    return false;
//            }
//            else
//                //线程钩子
//                _hookThread.Abort();
//            IsInstalled = false;
//            return true;
//        }
//    }
//}

//using System;
//using System.Threading;
//using System.Windows.Forms;
//using static FastWin32.NativeMethods;

//namespace FastWin32.Hook.WindowMessage
//{
//    /// <summary>
//    /// 转发消息
//    /// </summary>
//    internal sealed class MessageProxy
//    {
//        private IntPtr _hookHandle;

//        private Thread _hookThread;

//        /// <summary>
//        /// 启动代理
//        /// </summary>
//        /// <returns></returns>
//        private bool Install()
//        {
//            bool finished;
//            bool result;

//            finished = false;
//            result = false;
//            _hookThread = new Thread(() =>
//            {
//                //_hookHandle = SetWindowsHookEx(WH_KEYBOARD, KeyboardProc, IntPtr.Zero,TargetThreadId);
//                throw new NotImplementedException();
//                result = _hookHandle != IntPtr.Zero;
//                finished = true;
//                Application.Run();
//            })
//            {
//                IsBackground = true
//            };
//            _hookThread.Start();
//            while (!finished)
//                Thread.Sleep(0);
//            if (result)
//                return true;
//            else
//            {
//                _hookThread.Abort();
//                return false;
//            }
//        }

//        private IntPtr KeyboardProc(int nCode, size_t wParam, size_t lParam)
//        {
//            if (nCode < 0)
//                //如果nCode小于零，则钩子过程必须返回CallNextHookEx返回的值并且不对钩子消息做处理。如果3个事件均未被订阅，直接返回
//                return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
//            else
//            {
//                //if (keyboardHook.OnHookProc((uint)wParam, (uint)lParam, (uint)lParam))
//                //    return (IntPtr)(-1);
//                //else
//                //    return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
//                throw new NotImplementedException();
//            }
//        }

//        ///// <summary>
//        ///// 停止代理
//        ///// </summary>
//        ///// <returns></returns>
//        //public bool Uninstall()
//        //{
//        //    if (_hookThread.ThreadState == ThreadState.Running)
//        //        _hookThread.Abort();
//        //    return UnhookWindowsHookEx(_hookHandle);
//        //}

//        /// <summary>
//        /// 启动代理，注入DLL使用
//        /// </summary>
//        /// <param name="arg">参数</param>
//        /// <returns></returns>
//        public static int StartServer(string arg)
//        {
//            return new MessageProxy {  }.Install() ? 1 : 0;
//        }
//    }
//}


namespace FastWin32.Windowing
{
    /// <summary>
    /// 窗口
    /// </summary>
    public static unsafe class Window
    {
        /// <summary>
        /// 遍历窗口回调函数，继续遍历返回true，否则返回false
        /// </summary>
        /// <param name="windowHandle">窗口句柄</param>
        /// <returns></returns>
        public delegate bool EnumWindowsCallback(IntPtr windowHandle);

        /// <summary>
        /// 获取包含桌面ListView的句柄
        /// </summary>
        /// <returns></returns>
        public static IntPtr GetDesktopView()
        {
            IntPtr workerW;
            IntPtr programManager;
            IntPtr shell;

            workerW = IntPtr.Zero;
            shell = IntPtr.Zero;
            if (Environment.OSVersion.Version.Major >= 6)
            {
                //Vista及以上        
                NativeMethods.EnumWindows((windowHandle, lParam) =>
                {
                    shell = FindWindowEx(windowHandle, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (shell != IntPtr.Zero)
                    {
                        //如果当前窗口存在类名为SHELLDLL_DefView的子窗口
                        workerW = FindWindowEx(IntPtr.Zero, windowHandle, "WorkerW", null);
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);
            }
            else
            {
                //XP及以下
                programManager = GetShellWindow();
                //XP的SHELLDLL_DefView在Program Manager里
                shell = FindWindowEx(programManager, IntPtr.Zero, "SHELLDLL_DefView", null);
            }
            //先获取WorkerW
            return FindWindowEx(shell, IntPtr.Zero, "SysListView32", "FolderView");
        }

        /// <summary>
        /// 将窗口置顶并激活（单次，非永久），非直接调用Win32API SetForegroundWindow，成功率高
        /// </summary>
        /// <param name="windowHandle">窗口句柄</param>
        public static void SetForegroundWindow(IntPtr windowHandle)
        {
            if (!IsWindow(windowHandle))
                throw new ArgumentException("无效窗口句柄");

            uint currentThreadId;
            uint foregroundThreadId;

            currentThreadId = GetCurrentThreadId();
            //获取当前线程ID
            foregroundThreadId = GetWindowThreadProcessId(GetForegroundWindow(), null);
            //获取要附加到的线程的ID
            AttachThreadInput(currentThreadId, foregroundThreadId, true);
            //附加到线程
            NativeMethods.SetForegroundWindow(windowHandle);
            SetActiveWindow(windowHandle);
            SetFocus(windowHandle);
            AttachThreadInput(currentThreadId, foregroundThreadId, false);
            //分离
        }

        /// <summary>
        /// 查找窗口
        /// </summary>
        /// <param name="className">窗口类名</param>
        /// <param name="windowName">窗口标题</param>
        /// <returns></returns>
        public static IntPtr FindWindow(string className, string windowName)
        {
            return NativeMethods.FindWindow(className, windowName);
        }

        /// <summary>
        /// 查找窗口
        /// </summary>
        /// <param name="parentWindowHandle">父窗口句柄</param>
        /// <param name="afterWindowHandle">从此窗口之后开始查找（此窗口必须为父窗口的直接子窗口）</param>
        /// <param name="className">窗口类名</param>
        /// <param name="windowName">窗口标题</param>
        /// <returns></returns>
        public static IntPtr FindWindow(IntPtr parentWindowHandle, IntPtr afterWindowHandle, string className, string windowName)
        {
            return FindWindowEx(parentWindowHandle, afterWindowHandle, className, windowName);
        }

        /// <summary>
        /// 遍历所有顶级窗口
        /// </summary>
        /// <param name="callback">查找到窗口时的回调函数</param>
        /// <returns></returns>
        public static bool EnumWindows(EnumWindowsCallback callback)
        {
            if (callback == null)
                throw new ArgumentNullException();

            return NativeMethods.EnumWindows((windowHandle, lParam) => callback(windowHandle), IntPtr.Zero);
        }

        /// <summary>
        /// 遍历所有子窗口
        /// </summary>
        /// <param name="windowHandleParent">父窗口</param>
        /// <param name="callback">查找到窗口时的回调函数</param>
        /// <returns></returns>
        public static bool EnumChildWindows(IntPtr windowHandleParent, EnumWindowsCallback callback)
        {
            if (callback == null)
                throw new ArgumentNullException();

            return NativeMethods.EnumChildWindows(windowHandleParent, (windowHandle, lParam) => callback(windowHandle), IntPtr.Zero);
        }
    }
}
