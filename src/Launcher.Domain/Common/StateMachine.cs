// Copyright (c) Helsincy. All rights reserved.

using Launcher.Shared;

namespace Launcher.Domain.Common;

/// <summary>
/// 泛型状态机基类。管理状态转换及合法性验证。
/// 子类通过 DefineTransition 定义允许的状态转换图。
/// </summary>
public abstract class StateMachine<TState> where TState : notnull
{
    private readonly Dictionary<TState, HashSet<TState>> _transitions = new();

    public TState Current { get; private set; }

    protected StateMachine(TState initialState)
    {
        Current = initialState;
    }

    /// <summary>
    /// 定义从 from 到 to 的合法转换。
    /// </summary>
    protected void DefineTransition(TState from, TState to)
    {
        if (!_transitions.TryGetValue(from, out var targets))
        {
            targets = [];
            _transitions[from] = targets;
        }
        targets.Add(to);
    }

    /// <summary>
    /// 尝试转换到目标状态。非法转换返回失败 Result。
    /// </summary>
    public Result TransitionTo(TState target)
    {
        if (!_transitions.TryGetValue(Current, out var allowed) || !allowed.Contains(target))
        {
            return Result.Fail(new Error
            {
                Code = "SM_INVALID_TRANSITION",
                UserMessage = "操作目前不可用",
                TechnicalMessage = $"非法状态转换: {Current} → {target}",
                Severity = ErrorSeverity.Error
            });
        }

        var previous = Current;
        Current = target;
        OnTransitioned(previous, target);
        return Result.Ok();
    }

    /// <summary>
    /// 检查是否允许转换到目标状态。
    /// </summary>
    public bool CanTransitionTo(TState target)
    {
        return _transitions.TryGetValue(Current, out var allowed) && allowed.Contains(target);
    }

    /// <summary>
    /// 状态转换后的回调，子类可重写以执行副作用。
    /// </summary>
    protected virtual void OnTransitioned(TState fromState, TState toState) { }
}
