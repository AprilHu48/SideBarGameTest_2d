using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

public class TransparentBottomWindow : MonoBehaviour
{
    /*
     * 这段代码只在 Windows 独立运行版生效。
     * 
     * UNITY_STANDALONE_WIN 表示 Windows 平台；
     * !UNITY_EDITOR 表示不在 Unity 编辑器里执行。
     * 
     * 因为这些 Windows API 只对真正的 exe 窗口有效，
     * 在 Unity Editor 里运行没有意义，也可能出错。
     */
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR

    /*
     * DWM 透明扩展需要用到的结构体。
     * 
     * DWM = Desktop Window Manager，
     * 是 Windows 负责窗口合成、透明、阴影等效果的系统组件。
     */
    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int cxLeftWidth;     // 左侧扩展宽度
        public int cxRightWidth;    // 右侧扩展宽度
        public int cyTopHeight;     // 顶部扩展高度
        public int cyBottomHeight;  // 底部扩展高度
    }

    /*
     * 获取当前 Unity Player 的窗口句柄。
     * 
     * IntPtr 可以理解为一个“窗口 ID”。
     * 后面所有 Windows API 都需要通过这个句柄来操作 Unity 窗口。
     */
    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    /*
     * 获取屏幕尺寸。
     * 
     * GetSystemMetrics(0) 获取屏幕宽度；
     * GetSystemMetrics(1) 获取屏幕高度。
     */
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    /*
     * 获取窗口当前样式。
     * 
     * 窗口样式包括：
     * 是否有标题栏、是否有边框、是否可以缩放、是否透明等。
     */
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    /*
     * 修改窗口样式。
     * 
     * 这里我们会用它把 Unity 窗口改成：
     * - 无边框
     * - 支持透明
     */
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    /*
     * 设置窗口的位置、大小、层级。
     * 
     * 这里用于：
     * - 把窗口移动到屏幕底部
     * - 设置窗口宽高
     * - 让窗口置顶
     */
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd,              // 要操作的窗口
        IntPtr hWndInsertAfter,   // 窗口层级，比如是否置顶
        int X,                    // 窗口左上角 X 坐标
        int Y,                    // 窗口左上角 Y 坐标
        int cx,                   // 窗口宽度
        int cy,                   // 窗口高度
        uint uFlags               // 额外行为标记
    );

    /*
     * 让 Windows 的 DWM 把透明效果扩展到窗口客户区。
     * 
     * 简单说：
     * 它允许 Unity 窗口背景里的 alpha = 0 的部分真正显示桌面。
     */
    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref Margins pMarInset);

    /*
     * GWL_STYLE 表示修改普通窗口样式。
     * 
     * 例如标题栏、边框、最大化按钮、最小化按钮等。
     */
    private const int GWL_STYLE = -16;

    /*
     * GWL_EXSTYLE 表示修改扩展窗口样式。
     * 
     * 扩展样式里包含透明窗口、点击穿透、工具窗口等特殊能力。
     */
    private const int GWL_EXSTYLE = -20;

    /*
     * WS_POPUP 是一种无边框窗口样式。
     * 
     * 设置成这个后，Unity 窗口会去掉：
     * - 标题栏
     * - 边框
     * - 系统按钮
     */
    private const int WS_POPUP = unchecked((int)0x80000000);

    /*
     * WS_EX_LAYERED 表示窗口支持分层透明。
     * 
     * 没有这个，Unity 即使 Camera 背景 alpha = 0，
     * Windows 也不一定会把它当成真正透明。
     */
    private const int WS_EX_LAYERED = 0x00080000;

    /*
     * GetSystemMetrics 参数。
     */
    private const int SM_CXSCREEN = 0; // 屏幕宽度
    private const int SM_CYSCREEN = 1; // 屏幕高度

    /*
     * SetWindowPos 的参数。
     * 
     * SWP_SHOWWINDOW 表示设置完成后显示窗口。
     */
    private const uint SWP_SHOWWINDOW = 0x0040;

    /*
     * HWND_TOPMOST 表示窗口置顶。
     * 
     * 设置后，Unity 游戏条会显示在其他普通窗口上方。
     */
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

#endif

    /*
     * 羽化高度。
     * 
     * 比如 40 表示 Unity 窗口顶部留出 40 像素，
     * 这 40 像素用于做透明到游戏画面的渐变过渡。
     */
    public int featherHeight = 40;

    private IEnumerator Start()
    {
        yield return null;
        yield return null;
        /*
         * 游戏启动时设置窗口。
         * 
         * 注意：
         * 只有打包成 Windows exe 后才会真正执行。
         */
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        SetupWindow();
#endif
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR

    private void SetupWindow()
    {
        /*
         * 获取 Unity Player 当前窗口句柄。
         */
        IntPtr hwnd = GetActiveWindow();

        /*
         * 获取屏幕宽高。
         */
        int screenWidth = GetSystemMetrics(SM_CXSCREEN);
        int screenHeight = GetSystemMetrics(SM_CYSCREEN);

        /*
         * 游戏主体高度是屏幕高度的 1/5。
         */
        int gameHeight = screenHeight / 5;

        /*
         * 窗口真实高度 = 游戏高度 + 羽化高度。
         * 
         * 例如：
         * 屏幕高 1080；
         * 游戏高度 216；
         * 羽化高度 40；
         * 最终 Unity 窗口高度就是 256。
         */
        int windowHeight = gameHeight + featherHeight;

        /*
         * 窗口 X 坐标为 0，表示贴着屏幕左边。
         */
        int x = 0;

        /*
         * Windows 窗口坐标的 Y 是从屏幕顶部开始算的。
         * 
         * 所以要把窗口放到底部：
         * y = 屏幕高度 - 窗口高度
         */
        int y = screenHeight - windowHeight;

        /*
         * 把 Unity 窗口改成无边框弹出窗口。
         */
        SetWindowLong(hwnd, GWL_STYLE, WS_POPUP);

        /*
         * 读取当前扩展窗口样式。
         * 
         * 这里不要直接覆盖，而是先读取旧样式，
         * 然后追加 WS_EX_LAYERED。
         */
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

        /*
         * 给窗口增加“分层透明”能力。
         */
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);

        /*
         * 设置 DWM 透明扩展。
         * 
         * 四个值全部为 -1，表示把透明效果扩展到整个窗口。
         * 这样 Unity Camera 背景 alpha = 0 的区域才能显示桌面。
         */
        Margins margins = new Margins
        {
            cxLeftWidth = -1,
            cxRightWidth = -1,
            cyTopHeight = -1,
            cyBottomHeight = -1
        };

        DwmExtendFrameIntoClientArea(hwnd, ref margins);

        /*
         * 设置窗口最终位置、大小、层级。
         * 
         * 结果：
         * - 窗口宽度 = 整个屏幕宽度
         * - 窗口高度 = 屏幕 1/5 + 羽化高度
         * - 窗口位置 = 屏幕底部
         * - 窗口层级 = 置顶
         */
        SetWindowPos(
            hwnd,
            HWND_TOPMOST,
            x,
            y,
            screenWidth,
            windowHeight,
            SWP_SHOWWINDOW
        );
    }

#endif
}
