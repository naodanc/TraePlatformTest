using Serilog.Context;
using TraePlatform.Logging.Abstractions;

namespace TraePlatform.Logging;

/// <summary>
/// 日志上下文作用域。业务/模块用它在一段代码内统一注入 Module、TraceId、UserId 等
/// 约定属性，这些属性会被自动提取为独立列，从而支持按模块与链路检索。
/// </summary>
public static class LogContextScope
{
    /// <summary>压入日志上下文属性，返回可释放作用域。</summary>
    public static IDisposable Push(string? module = null, string? traceId = null, string? userId = null)
    {
        var handles = new List<IDisposable>(3);

        if (!string.IsNullOrEmpty(module))
        {
            handles.Add(LogContext.PushProperty(LogProperties.Module, module));
        }
        if (!string.IsNullOrEmpty(traceId))
        {
            handles.Add(LogContext.PushProperty(LogProperties.TraceId, traceId));
        }
        if (!string.IsNullOrEmpty(userId))
        {
            handles.Add(LogContext.PushProperty(LogProperties.UserId, userId));
        }

        return new Scope(handles);
    }

    private sealed class Scope : IDisposable
    {
        private readonly List<IDisposable> _handles;

        public Scope(List<IDisposable> handles) => _handles = handles;

        public void Dispose()
        {
            for (var i = _handles.Count - 1; i >= 0; i--)
            {
                _handles[i].Dispose();
            }
        }
    }
}
