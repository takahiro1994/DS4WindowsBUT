/*
DS4Windows
Copyright (C) 2023  Travis Nickles

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DS4Windows
{
    public enum MacroActionType
    {
        KeyPress,
        KeyRelease,
        MouseClick,
        MouseMove,
        Wait,
        Repeat,
        Conditional,
        Variable,
        GamepadInput,
        Custom
    }

    public enum MacroConditionType
    {
        ButtonPressed,
        ButtonReleased,
        BatteryLevel,
        GameRunning,
        TimeOfDay,
        RandomChance,
        VariableEquals,
        Custom
    }

    public class MacroAction
    {
        public MacroActionType Type { get; set; }
        public string Parameter { get; set; }
        public int Duration { get; set; } = 50; // Default 50ms
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    public class MacroCondition
    {
        public MacroConditionType Type { get; set; }
        public string Parameter { get; set; }
        public object ExpectedValue { get; set; }
        public bool Negate { get; set; } = false;
    }

    public class ConditionalMacroAction : MacroAction
    {
        public MacroCondition Condition { get; set; }
        public List<MacroAction> TrueActions { get; set; } = new List<MacroAction>();
        public List<MacroAction> FalseActions { get; set; } = new List<MacroAction>();
    }

    public class MacroSequence
    {
        public string Name { get; set; }
        public List<MacroAction> Actions { get; set; } = new List<MacroAction>();
        public bool IsActive { get; set; }
        public bool Loop { get; set; }
        public int LoopCount { get; set; } = 1;
        public TimeSpan Delay { get; set; } = TimeSpan.Zero;
        public Dictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();
        public DateTime LastExecuted { get; set; }
        public int ExecutionCount { get; set; }
    }

    public class MacroTrigger
    {
        public string Name { get; set; }
        public DS4Controls TriggerButton { get; set; }
        public MacroTriggerType TriggerType { get; set; }
        public MacroSequence Sequence { get; set; }
        public bool IsEnabled { get; set; } = true;
        public TimeSpan Cooldown { get; set; } = TimeSpan.Zero;
        public DateTime LastTriggered { get; set; }
    }

    public enum MacroTriggerType
    {
        OnPress,
        OnRelease,
        OnHold,
        WhileHeld,
        OnDoublePress,
        OnLongPress,
        Combo
    }

    public class MacroExecutionContext
    {
        public DS4Device Device { get; set; }
        public DS4State CurrentState { get; set; }
        public Dictionary<string, object> GlobalVariables { get; set; } = new Dictionary<string, object>();
        public CancellationToken CancellationToken { get; set; }
    }

    public class AdvancedMacroSystem
    {
        private readonly DS4Device device;
        private readonly Dictionary<string, MacroSequence> macroSequences;
        private readonly List<MacroTrigger> macroTriggers;
        private readonly Dictionary<string, object> globalVariables;
        private readonly Dictionary<string, Task> runningMacros;
        private readonly Random random;

        public event EventHandler<MacroExecutedEventArgs> MacroExecuted;
        public event EventHandler<MacroErrorEventArgs> MacroError;

        public IReadOnlyDictionary<string, MacroSequence> MacroSequences => macroSequences;
        public IReadOnlyList<MacroTrigger> MacroTriggers => macroTriggers;

        public AdvancedMacroSystem(DS4Device device)
        {
            this.device = device ?? throw new ArgumentNullException(nameof(device));
            this.macroSequences = new Dictionary<string, MacroSequence>();
            this.macroTriggers = new List<MacroTrigger>();
            this.globalVariables = new Dictionary<string, object>();
            this.runningMacros = new Dictionary<string, Task>();
            this.random = new Random();

            InitializeBuiltInMacros();
        }

        private void InitializeBuiltInMacros()
        {
            // Create some common macro templates
            CreateBatteryCheckMacro();
            CreateQuickComboMacro();
            CreateAdaptiveMacro();
        }

        private void CreateBatteryCheckMacro()
        {
            var batteryMacro = new MacroSequence
            {
                Name = "BatteryCheck",
                Actions = new List<MacroAction>
                {
                    new ConditionalMacroAction
                    {
                        Type = MacroActionType.Conditional,
                        Condition = new MacroCondition
                        {
                            Type = MacroConditionType.BatteryLevel,
                            ExpectedValue = 20
                        },
                        TrueActions = new List<MacroAction>
                        {
                            new MacroAction { Type = MacroActionType.Custom, Parameter = "flash_red_lightbar" },
                            new MacroAction { Type = MacroActionType.Custom, Parameter = "vibrate_warning" }
                        },
                        FalseActions = new List<MacroAction>
                        {
                            new MacroAction { Type = MacroActionType.Custom, Parameter = "show_battery_level" }
                        }
                    }
                }
            };

            macroSequences["BatteryCheck"] = batteryMacro;
        }

        private void CreateQuickComboMacro()
        {
            var comboMacro = new MacroSequence
            {
                Name = "QuickCombo",
                Actions = new List<MacroAction>
                {
                    new MacroAction { Type = MacroActionType.GamepadInput, Parameter = "Cross", Duration = 50 },
                    new MacroAction { Type = MacroActionType.Wait, Duration = 100 },
                    new MacroAction { Type = MacroActionType.GamepadInput, Parameter = "Square", Duration = 50 },
                    new MacroAction { Type = MacroActionType.Wait, Duration = 100 },
                    new MacroAction { Type = MacroActionType.GamepadInput, Parameter = "Triangle", Duration = 50 }
                }
            };

            macroSequences["QuickCombo"] = comboMacro;
        }

        private void CreateAdaptiveMacro()
        {
            var adaptiveMacro = new MacroSequence
            {
                Name = "AdaptiveAction",
                Actions = new List<MacroAction>
                {
                    new ConditionalMacroAction
                    {
                        Type = MacroActionType.Conditional,
                        Condition = new MacroCondition
                        {
                            Type = MacroConditionType.TimeOfDay,
                            ExpectedValue = "night" // 6 PM to 6 AM
                        },
                        TrueActions = new List<MacroAction>
                        {
                            new MacroAction { Type = MacroActionType.Custom, Parameter = "low_brightness_mode" },
                            new MacroAction { Type = MacroActionType.Custom, Parameter = "reduced_vibration" }
                        },
                        FalseActions = new List<MacroAction>
                        {
                            new MacroAction { Type = MacroActionType.Custom, Parameter = "normal_brightness_mode" },
                            new MacroAction { Type = MacroActionType.Custom, Parameter = "normal_vibration" }
                        }
                    }
                }
            };

            macroSequences["AdaptiveAction"] = adaptiveMacro;
        }

        public void CreateMacro(string name, List<MacroAction> actions, bool loop = false, int loopCount = 1)
        {
            var macro = new MacroSequence
            {
                Name = name,
                Actions = new List<MacroAction>(actions),
                Loop = loop,
                LoopCount = loopCount,
                IsActive = true
            };

            macroSequences[name] = macro;
            AppLogger.LogToGui($"Created macro: {name} with {actions.Count} actions", false);
        }

        public void CreateTrigger(string name, DS4Controls button, MacroTriggerType triggerType, string macroName, TimeSpan cooldown = default)
        {
            if (!macroSequences.TryGetValue(macroName, out var sequence))
            {
                AppLogger.LogToGui($"Macro '{macroName}' not found for trigger '{name}'", true);
                return;
            }

            var trigger = new MacroTrigger
            {
                Name = name,
                TriggerButton = button,
                TriggerType = triggerType,
                Sequence = sequence,
                Cooldown = cooldown,
                IsEnabled = true
            };

            macroTriggers.Add(trigger);
            AppLogger.LogToGui($"Created trigger: {name} for {button} -> {macroName}", false);
        }

        public async Task<bool> ExecuteMacroAsync(string macroName, MacroExecutionContext context = null)
        {
            if (!macroSequences.TryGetValue(macroName, out var macro))
            {
                AppLogger.LogToGui($"Macro '{macroName}' not found", true);
                return false;
            }

            context = context ?? new MacroExecutionContext { Device = device };
            
            try
            {
                // Check if macro is already running
                if (runningMacros.ContainsKey(macroName))
                {
                    AppLogger.LogToGui($"Macro '{macroName}' is already running", false);
                    return false;
                }

                // Start macro execution
                var executionTask = ExecuteMacroSequence(macro, context);
                runningMacros[macroName] = executionTask;

                var result = await executionTask;
                
                macro.LastExecuted = DateTime.UtcNow;
                macro.ExecutionCount++;
                
                MacroExecuted?.Invoke(this, new MacroExecutedEventArgs(macro, result));
                return result;
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error executing macro '{macroName}': {ex.Message}", true);
                MacroError?.Invoke(this, new MacroErrorEventArgs(macro, ex));
                return false;
            }
            finally
            {
                runningMacros.Remove(macroName);
            }
        }

        private async Task<bool> ExecuteMacroSequence(MacroSequence macro, MacroExecutionContext context)
        {
            var loopsToExecute = macro.Loop ? (macro.LoopCount <= 0 ? int.MaxValue : macro.LoopCount) : 1;
            
            for (int loop = 0; loop < loopsToExecute && !context.CancellationToken.IsCancellationRequested; loop++)
            {
                // Apply delay between loops
                if (loop > 0 && macro.Delay > TimeSpan.Zero)
                {
                    await Task.Delay(macro.Delay, context.CancellationToken);
                }

                foreach (var action in macro.Actions)
                {
                    if (context.CancellationToken.IsCancellationRequested)
                        break;

                    await ExecuteMacroAction(action, macro, context);
                }
            }

            return true;
        }

        private async Task ExecuteMacroAction(MacroAction action, MacroSequence macro, MacroExecutionContext context)
        {
            try
            {
                switch (action.Type)
                {
                    case MacroActionType.KeyPress:
                        await ExecuteKeyPress(action.Parameter, action.Duration);
                        break;

                    case MacroActionType.KeyRelease:
                        await ExecuteKeyRelease(action.Parameter);
                        break;

                    case MacroActionType.MouseClick:
                        await ExecuteMouseClick(action.Parameter);
                        break;

                    case MacroActionType.MouseMove:
                        await ExecuteMouseMove(action.Parameter);
                        break;

                    case MacroActionType.Wait:
                        await Task.Delay(action.Duration, context.CancellationToken);
                        break;

                    case MacroActionType.GamepadInput:
                        await ExecuteGamepadInput(action.Parameter, action.Duration);
                        break;

                    case MacroActionType.Conditional:
                        if (action is ConditionalMacroAction conditionalAction)
                        {
                            await ExecuteConditionalAction(conditionalAction, macro, context);
                        }
                        break;

                    case MacroActionType.Variable:
                        ExecuteVariableAction(action, macro);
                        break;

                    case MacroActionType.Custom:
                        await ExecuteCustomAction(action.Parameter, context);
                        break;
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error executing macro action {action.Type}: {ex.Message}", true);
            }
        }

        private async Task ExecuteConditionalAction(ConditionalMacroAction action, MacroSequence macro, MacroExecutionContext context)
        {
            var conditionMet = EvaluateCondition(action.Condition, context);
            if (action.Condition.Negate)
                conditionMet = !conditionMet;

            var actionsToExecute = conditionMet ? action.TrueActions : action.FalseActions;
            
            foreach (var subAction in actionsToExecute)
            {
                if (context.CancellationToken.IsCancellationRequested)
                    break;
                    
                await ExecuteMacroAction(subAction, macro, context);
            }
        }

        private bool EvaluateCondition(MacroCondition condition, MacroExecutionContext context)
        {
            switch (condition.Type)
            {
                case MacroConditionType.ButtonPressed:
                    return IsButtonPressed(condition.Parameter, context.CurrentState);

                case MacroConditionType.ButtonReleased:
                    return !IsButtonPressed(condition.Parameter, context.CurrentState);

                case MacroConditionType.BatteryLevel:
                    if (condition.ExpectedValue is int threshold)
                        return device.getBattery() <= threshold;
                    break;

                case MacroConditionType.GameRunning:
                    return IsGameRunning(condition.Parameter);

                case MacroConditionType.TimeOfDay:
                    return CheckTimeOfDay(condition.Parameter);

                case MacroConditionType.RandomChance:
                    if (condition.ExpectedValue is double chance)
                        return random.NextDouble() < chance;
                    break;

                case MacroConditionType.VariableEquals:
                    return CheckVariableEquals(condition.Parameter, condition.ExpectedValue);
            }

            return false;
        }

        private bool IsButtonPressed(string buttonName, DS4State state)
        {
            if (state == null) return false;

            return buttonName.ToLower() switch
            {
                "cross" => state.Cross,
                "triangle" => state.Triangle,
                "circle" => state.Circle,
                "square" => state.Square,
                "l1" => state.L1,
                "r1" => state.R1,
                "l2" => state.L2Btn,
                "r2" => state.R2Btn,
                "l3" => state.L3,
                "r3" => state.R3,
                "options" => state.Options,
                "share" => state.Share,
                "ps" => state.PS,
                _ => false
            };
        }

        private bool IsGameRunning(string gameName)
        {
            try
            {
                var processes = System.Diagnostics.Process.GetProcessesByName(gameName);
                return processes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private bool CheckTimeOfDay(string timeCondition)
        {
            var now = DateTime.Now.TimeOfDay;
            
            return timeCondition.ToLower() switch
            {
                "morning" => now >= TimeSpan.FromHours(6) && now < TimeSpan.FromHours(12),
                "afternoon" => now >= TimeSpan.FromHours(12) && now < TimeSpan.FromHours(18),
                "evening" => now >= TimeSpan.FromHours(18) && now < TimeSpan.FromHours(22),
                "night" => now >= TimeSpan.FromHours(22) || now < TimeSpan.FromHours(6),
                _ => false
            };
        }

        private bool CheckVariableEquals(string variableName, object expectedValue)
        {
            if (globalVariables.TryGetValue(variableName, out var value))
            {
                return value?.Equals(expectedValue) == true;
            }
            return false;
        }

        private void ExecuteVariableAction(MacroAction action, MacroSequence macro)
        {
            var parts = action.Parameter.Split('=');
            if (parts.Length == 2)
            {
                var variableName = parts[0].Trim();
                var variableValue = parts[1].Trim();
                
                // Try to parse as different types
                if (int.TryParse(variableValue, out var intValue))
                    globalVariables[variableName] = intValue;
                else if (double.TryParse(variableValue, out var doubleValue))
                    globalVariables[variableName] = doubleValue;
                else if (bool.TryParse(variableValue, out var boolValue))
                    globalVariables[variableName] = boolValue;
                else
                    globalVariables[variableName] = variableValue;
                    
                AppLogger.LogToGui($"Set variable {variableName} = {variableValue}", false);
            }
        }

        private async Task ExecuteKeyPress(string keyName, int duration)
        {
            // This would integrate with existing keyboard input system
            AppLogger.LogToGui($"Key press: {keyName} for {duration}ms", false);
            await Task.Delay(duration);
        }

        private async Task ExecuteKeyRelease(string keyName)
        {
            AppLogger.LogToGui($"Key release: {keyName}", false);
            await Task.Delay(10);
        }

        private async Task ExecuteMouseClick(string button)
        {
            AppLogger.LogToGui($"Mouse click: {button}", false);
            await Task.Delay(50);
        }

        private async Task ExecuteMouseMove(string coordinates)
        {
            AppLogger.LogToGui($"Mouse move: {coordinates}", false);
            await Task.Delay(10);
        }

        private async Task ExecuteGamepadInput(string input, int duration)
        {
            AppLogger.LogToGui($"Gamepad input: {input} for {duration}ms", false);
            await Task.Delay(duration);
        }

        private async Task ExecuteCustomAction(string actionName, MacroExecutionContext context)
        {
            switch (actionName.ToLower())
            {
                case "flash_red_lightbar":
                    // Flash lightbar red
                    break;
                case "vibrate_warning":
                    // Trigger warning vibration
                    break;
                case "show_battery_level":
                    // Show battery level via lightbar
                    break;
                case "low_brightness_mode":
                    // Reduce lightbar brightness
                    break;
                default:
                    AppLogger.LogToGui($"Unknown custom action: {actionName}", true);
                    break;
            }
            
            await Task.Delay(100);
        }

        public void ProcessControllerState(DS4State currentState)
        {
            foreach (var trigger in macroTriggers.Where(t => t.IsEnabled))
            {
                // Check cooldown
                if ((DateTime.UtcNow - trigger.LastTriggered) < trigger.Cooldown)
                    continue;

                if (ShouldTriggerMacro(trigger, currentState))
                {
                    trigger.LastTriggered = DateTime.UtcNow;
                    var context = new MacroExecutionContext
                    {
                        Device = device,
                        CurrentState = currentState,
                        GlobalVariables = globalVariables
                    };

                    // Execute macro asynchronously
                    Task.Run(() => ExecuteMacroAsync(trigger.Sequence.Name, context));
                }
            }
        }

        private bool ShouldTriggerMacro(MacroTrigger trigger, DS4State currentState)
        {
            // This would need access to previous state for proper trigger detection
            // For now, simplified implementation
            var buttonPressed = IsButtonPressed(trigger.TriggerButton.ToString(), currentState);
            
            return trigger.TriggerType switch
            {
                MacroTriggerType.OnPress => buttonPressed,
                MacroTriggerType.WhileHeld => buttonPressed,
                _ => false
            };
        }

        public void StopMacro(string macroName)
        {
            if (runningMacros.TryGetValue(macroName, out var task))
            {
                // Would need CancellationTokenSource to properly cancel
                AppLogger.LogToGui($"Stopping macro: {macroName}", false);
            }
        }

        public void StopAllMacros()
        {
            foreach (var macroName in runningMacros.Keys.ToList())
            {
                StopMacro(macroName);
            }
        }

        public List<string> GetRunningMacros()
        {
            return runningMacros.Keys.ToList();
        }
    }

    // Event argument classes
    public class MacroExecutedEventArgs : EventArgs
    {
        public MacroSequence Macro { get; }
        public bool Success { get; }

        public MacroExecutedEventArgs(MacroSequence macro, bool success)
        {
            Macro = macro;
            Success = success;
        }
    }

    public class MacroErrorEventArgs : EventArgs
    {
        public MacroSequence Macro { get; }
        public Exception Error { get; }

        public MacroErrorEventArgs(MacroSequence macro, Exception error)
        {
            Macro = macro;
            Error = error;
        }
    }
}
