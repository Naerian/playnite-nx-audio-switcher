using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace PlayniteAudioSwitcher
{
    internal sealed class StandardHidBatteryProvider : IAudioDeviceBatteryProvider
    {
        private const ushort GenericDeviceControlsPage = 0x06;
        private const ushort BatteryStrengthUsage = 0x20;
        private const ushort BatterySystemPage = 0x85;
        private const ushort RelativeStateOfChargeUsage = 0x64;
        private const ushort AbsoluteStateOfChargeUsage = 0x65;
        private const ushort RemainingCapacityUsage = 0x66;
        private const ushort FullChargeCapacityUsage = 0x67;
        private const ushort ChargingUsage = 0x44;
        private const string ContainerIdProperty = "System.Devices.ContainerId";

        private static readonly Type DeviceInformationType = WindowsType("Windows.Devices.Enumeration.DeviceInformation");
        private static readonly Type DeviceInformationKindType = WindowsType("Windows.Devices.Enumeration.DeviceInformationKind");
        private static readonly Type HidDeviceType = WindowsType("Windows.Devices.HumanInterfaceDevice.HidDevice");
        private static readonly Type HidReportTypeType = WindowsType("Windows.Devices.HumanInterfaceDevice.HidReportType");
        private static readonly Type FileAccessModeType = WindowsType("Windows.Storage.FileAccessMode");

        private readonly object diagnosticsLock = new object();
        private IReadOnlyList<string> diagnostics = Array.Empty<string>();

        public string Name => "HID.Standard";

        public IReadOnlyList<string> GetDiagnostics()
        {
            lock (diagnosticsLock)
            {
                return diagnostics.ToList();
            }
        }

        public async Task<IReadOnlyDictionary<Guid, AudioDeviceBatteryInfo>> ReadAsync(IEnumerable<Guid> containerIds)
        {
            var requestedIds = new HashSet<Guid>((containerIds ?? Enumerable.Empty<Guid>()).Where(id => id != Guid.Empty));
            var result = new Dictionary<Guid, AudioDeviceBatteryInfo>();
            var newDiagnostics = new List<string>();
            if (requestedIds.Count == 0 || DeviceInformationType == null || DeviceInformationKindType == null ||
                HidDeviceType == null || HidReportTypeType == null || FileAccessModeType == null)
            {
                SetDiagnostics(newDiagnostics);
                return result;
            }

            try
            {
                var interfaces = await FindHidInterfacesAsync().ConfigureAwait(false);
                foreach (var deviceInformation in interfaces)
                {
                    if (!TryGetContainerId(deviceInformation, out var containerId) || !requestedIds.Contains(containerId))
                    {
                        continue;
                    }

                    var deviceName = GetProperty<string>(deviceInformation, "Name") ?? string.Empty;
                    var deviceId = GetProperty<string>(deviceInformation, "Id");
                    if (string.IsNullOrWhiteSpace(deviceId))
                    {
                        continue;
                    }

                    object hidDevice = null;
                    try
                    {
                        hidDevice = await OpenHidDeviceAsync(deviceId).ConfigureAwait(false);
                        if (hidDevice == null)
                        {
                            newDiagnostics.Add($"HID container={containerId:B} name=\"{deviceName}\" access=unavailable");
                            continue;
                        }

                        var vendorId = GetProperty<ushort>(hidDevice, "VendorId");
                        var productId = GetProperty<ushort>(hidDevice, "ProductId");
                        var usagePage = GetProperty<ushort>(hidDevice, "UsagePage");
                        var usageId = GetProperty<ushort>(hidDevice, "UsageId");
                        var controls = GetBatteryControlDescriptions(hidDevice);
                        newDiagnostics.Add(
                            $"HID container={containerId:B} vid={vendorId:X4} pid={productId:X4} top={usagePage:X4}:{usageId:X4} name=\"{deviceName}\" batteryControls={FormatControls(controls)}");

                        if (result.ContainsKey(containerId) || controls.Count == 0)
                        {
                            continue;
                        }

                        var reports = new Dictionary<string, object>();
                        var percent = await ReadPercentAsync(hidDevice, controls, reports).ConfigureAwait(false);
                        if (!percent.HasValue)
                        {
                            continue;
                        }

                        var isCharging = await ReadChargingAsync(hidDevice, reports).ConfigureAwait(false);
                        result[containerId] = new AudioDeviceBatteryInfo
                        {
                            Percent = Math.Max(0, Math.Min(100, percent.Value)),
                            IsCharging = isCharging,
                            Source = Name
                        };
                    }
                    catch (Exception ex)
                    {
                        newDiagnostics.Add($"HID container={containerId:B} name=\"{deviceName}\" error={ex.GetType().Name}");
                    }
                    finally
                    {
                        (hidDevice as IDisposable)?.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                newDiagnostics.Add($"HID enumeration error={ex.GetType().Name}: {ex.Message}");
            }

            SetDiagnostics(newDiagnostics);
            return result;
        }

        private static async Task<List<object>> FindHidInterfacesAsync()
        {
            var findMethod = DeviceInformationType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method => method.Name == "FindAllAsync" && method.GetParameters().Length == 3 &&
                    method.GetParameters()[2].ParameterType == DeviceInformationKindType);
            if (findMethod == null)
            {
                return new List<object>();
            }

            var selectorMethod = HidDeviceType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method => method.Name == "GetDeviceSelector" && method.GetParameters().Length == 2);
            if (selectorMethod == null)
            {
                return new List<object>();
            }

            var topLevelUsages = new[]
            {
                Tuple.Create((ushort)0x01, (ushort)0x02), // Mouse
                Tuple.Create((ushort)0x01, (ushort)0x04), // Joystick
                Tuple.Create((ushort)0x01, (ushort)0x05), // Gamepad
                Tuple.Create((ushort)0x01, (ushort)0x06), // Keyboard
                Tuple.Create((ushort)0x01, (ushort)0x07), // Keypad
                Tuple.Create((ushort)0x01, (ushort)0x08), // Multi-axis controller
                Tuple.Create((ushort)0x06, (ushort)0x20), // Generic battery strength
                Tuple.Create((ushort)0x0B, (ushort)0x01), // Telephony
                Tuple.Create((ushort)0x0C, (ushort)0x01), // Consumer control
                Tuple.Create((ushort)0x0D, (ushort)0x01), // Digitizer
                Tuple.Create((ushort)0x0D, (ushort)0x02), // Pen
                Tuple.Create((ushort)0x0D, (ushort)0x04), // Touchscreen
                Tuple.Create((ushort)0x0D, (ushort)0x05), // Touchpad
                Tuple.Create((ushort)0x84, (ushort)0x04), // UPS
                Tuple.Create((ushort)0x84, (ushort)0x05)  // Power supply
            };
            var deviceInterfaceKind = Enum.Parse(DeviceInformationKindType, "DeviceInterface");
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var usage in topLevelUsages)
            {
                var selector = selectorMethod.Invoke(null, new object[] { usage.Item1, usage.Item2 }) as string;
                var operation = findMethod.Invoke(null, new object[]
                {
                    selector,
                    new[] { ContainerIdProperty },
                    deviceInterfaceKind
                });
                var collection = await AwaitWinRtAsync(operation, findMethod.ReturnType.GetGenericArguments()[0]).ConfigureAwait(false) as IEnumerable;
                foreach (var item in collection?.Cast<object>() ?? Enumerable.Empty<object>())
                {
                    var id = GetProperty<string>(item, "Id");
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        result[id] = item;
                    }
                }
            }

            return result.Values.ToList();
        }

        private static async Task<object> OpenHidDeviceAsync(string deviceId)
        {
            var method = HidDeviceType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(candidate => candidate.Name == "FromIdAsync" && candidate.GetParameters().Length == 2);
            if (method == null)
            {
                return null;
            }

            var readMode = Enum.Parse(FileAccessModeType, "Read");
            return await AwaitWinRtAsync(
                method.Invoke(null, new[] { (object)deviceId, readMode }),
                method.ReturnType.GetGenericArguments()[0]).ConfigureAwait(false);
        }

        private static List<object> GetBatteryControlDescriptions(object hidDevice)
        {
            var result = new List<object>();
            foreach (var reportType in new[] { "Input", "Feature" })
            {
                AddDescriptions(hidDevice, result, reportType, GenericDeviceControlsPage, BatteryStrengthUsage);
                AddDescriptions(hidDevice, result, reportType, BatterySystemPage, RelativeStateOfChargeUsage);
                AddDescriptions(hidDevice, result, reportType, BatterySystemPage, AbsoluteStateOfChargeUsage);
                AddDescriptions(hidDevice, result, reportType, BatterySystemPage, RemainingCapacityUsage);
                AddDescriptions(hidDevice, result, reportType, BatterySystemPage, FullChargeCapacityUsage);
            }

            return result;
        }

        private static void AddDescriptions(object hidDevice, ICollection<object> destination, string reportTypeName, ushort usagePage, ushort usageId)
        {
            try
            {
                var reportType = Enum.Parse(HidReportTypeType, reportTypeName);
                var descriptions = HidDeviceType.GetMethod("GetNumericControlDescriptions")?
                    .Invoke(hidDevice, new[] { reportType, (object)usagePage, usageId }) as IEnumerable;
                if (descriptions != null)
                {
                    foreach (var description in descriptions)
                    {
                        destination.Add(description);
                    }
                }
            }
            catch
            {
            }
        }

        private static async Task<int?> ReadPercentAsync(object hidDevice, IReadOnlyList<object> controls, IDictionary<string, object> reports)
        {
            foreach (var target in new[]
            {
                Tuple.Create(GenericDeviceControlsPage, BatteryStrengthUsage),
                Tuple.Create(BatterySystemPage, RelativeStateOfChargeUsage),
                Tuple.Create(BatterySystemPage, AbsoluteStateOfChargeUsage)
            })
            {
                var description = controls.FirstOrDefault(control =>
                    GetProperty<ushort>(control, "UsagePage") == target.Item1 &&
                    GetProperty<ushort>(control, "UsageId") == target.Item2);
                var value = await ReadNumericControlAsync(hidDevice, description, reports).ConfigureAwait(false);
                if (value.HasValue)
                {
                    return NormalizePercent(value.Value, description);
                }
            }

            var remainingDescription = controls.FirstOrDefault(control =>
                GetProperty<ushort>(control, "UsagePage") == BatterySystemPage &&
                GetProperty<ushort>(control, "UsageId") == RemainingCapacityUsage);
            var fullDescription = controls.FirstOrDefault(control =>
                GetProperty<ushort>(control, "UsagePage") == BatterySystemPage &&
                GetProperty<ushort>(control, "UsageId") == FullChargeCapacityUsage);
            var remaining = await ReadNumericControlAsync(hidDevice, remainingDescription, reports).ConfigureAwait(false);
            var full = await ReadNumericControlAsync(hidDevice, fullDescription, reports).ConfigureAwait(false);
            if (remaining.HasValue && full > 0)
            {
                return (int)Math.Round(Math.Max(0d, Math.Min(1d, remaining.Value / (double)full.Value)) * 100d);
            }

            return null;
        }

        private static async Task<long?> ReadNumericControlAsync(object hidDevice, object description, IDictionary<string, object> reports)
        {
            if (description == null)
            {
                return null;
            }

            var report = await GetReportAsync(hidDevice, description, reports).ConfigureAwait(false);
            if (report == null)
            {
                return null;
            }

            try
            {
                var control = report.GetType().GetMethod("GetNumericControlByDescription")?.Invoke(report, new[] { description });
                return control == null ? (long?)null : GetProperty<long>(control, "Value");
            }
            catch
            {
                return null;
            }
        }

        private static async Task<bool> ReadChargingAsync(object hidDevice, IDictionary<string, object> reports)
        {
            foreach (var reportTypeName in new[] { "Input", "Feature" })
            {
                try
                {
                    var reportType = Enum.Parse(HidReportTypeType, reportTypeName);
                    var descriptions = HidDeviceType.GetMethod("GetBooleanControlDescriptions")?
                        .Invoke(hidDevice, new[] { reportType, (object)BatterySystemPage, ChargingUsage }) as IEnumerable;
                    foreach (var description in descriptions ?? Array.Empty<object>())
                    {
                        var report = await GetReportAsync(hidDevice, description, reports).ConfigureAwait(false);
                        var control = report?.GetType().GetMethod("GetBooleanControlByDescription")?.Invoke(report, new[] { description });
                        if (control != null && GetProperty<bool>(control, "IsActive"))
                        {
                            return true;
                        }
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        private static async Task<object> GetReportAsync(object hidDevice, object description, IDictionary<string, object> reports)
        {
            var reportType = GetProperty<object>(description, "ReportType")?.ToString();
            var reportId = GetProperty<ushort>(description, "ReportId");
            var key = $"{reportType}:{reportId}";
            if (reports.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var methodName = string.Equals(reportType, "Feature", StringComparison.OrdinalIgnoreCase)
                ? "GetFeatureReportAsync"
                : "GetInputReportAsync";
            var method = HidDeviceType.GetMethods().FirstOrDefault(candidate =>
                candidate.Name == methodName && candidate.GetParameters().Length == 1);
            if (method == null)
            {
                return null;
            }

            try
            {
                var report = await AwaitWinRtAsync(
                    method.Invoke(hidDevice, new object[] { reportId }),
                    method.ReturnType.GetGenericArguments()[0]).ConfigureAwait(false);
                reports[key] = report;
                return report;
            }
            catch
            {
                reports[key] = null;
                return null;
            }
        }

        private static int NormalizePercent(long value, object description)
        {
            var minimum = GetProperty<int>(description, "LogicalMinimum");
            var maximum = GetProperty<int>(description, "LogicalMaximum");
            if (maximum > minimum)
            {
                var ratio = (value - minimum) / (double)(maximum - minimum);
                return (int)Math.Round(Math.Max(0d, Math.Min(1d, ratio)) * 100d);
            }

            return (int)Math.Max(0, Math.Min(100, value));
        }

        private static string FormatControls(IEnumerable<object> controls)
        {
            var values = controls.Select(control =>
                $"{GetProperty<ushort>(control, "UsagePage"):X4}:{GetProperty<ushort>(control, "UsageId"):X4}/{GetProperty<object>(control, "ReportType")}/{GetProperty<ushort>(control, "ReportId"):X2}")
                .Distinct()
                .ToList();
            return values.Count == 0 ? "none" : string.Join(",", values);
        }

        private static bool TryGetContainerId(object deviceInformation, out Guid containerId)
        {
            containerId = Guid.Empty;
            try
            {
                var properties = GetProperty<object>(deviceInformation, "Properties") as IReadOnlyDictionary<string, object>;
                if (properties == null || !properties.TryGetValue(ContainerIdProperty, out var raw) || raw == null)
                {
                    return false;
                }

                if (raw is Guid guid)
                {
                    containerId = guid;
                    return guid != Guid.Empty;
                }

                return Guid.TryParse(raw.ToString(), out containerId) && containerId != Guid.Empty;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<object> AwaitWinRtAsync(object operation, Type resultType)
        {
            if (operation == null || resultType == null)
            {
                return null;
            }
            var extensionsType = typeof(System.WindowsRuntimeSystemExtensions);
            var asTaskMethod = extensionsType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method => method.Name == "AsTask" && method.IsGenericMethodDefinition &&
                    method.GetGenericArguments().Length == 1 && method.GetParameters().Length == 1 &&
                    method.GetParameters()[0].ParameterType.IsGenericType &&
                    method.GetParameters()[0].ParameterType.GetGenericTypeDefinition().FullName == "Windows.Foundation.IAsyncOperation`1");
            var task = asTaskMethod?.MakeGenericMethod(resultType).Invoke(null, new[] { operation }) as Task;
            if (task == null)
            {
                return null;
            }

            await task.ConfigureAwait(false);
            return task.GetType().GetProperty("Result")?.GetValue(task);
        }

        private void SetDiagnostics(IReadOnlyList<string> values)
        {
            lock (diagnosticsLock)
            {
                diagnostics = values ?? Array.Empty<string>();
            }
        }

        private static Type WindowsType(string name)
        {
            return Type.GetType($"{name}, Windows, ContentType=WindowsRuntime", false);
        }

        private static T GetProperty<T>(object instance, string propertyName)
        {
            if (instance == null)
            {
                return default(T);
            }

            var value = instance.GetType().GetProperty(propertyName)?.GetValue(instance);
            if (value == null)
            {
                return default(T);
            }

            if (value is T typed)
            {
                return typed;
            }

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default(T);
            }
        }
    }
}
