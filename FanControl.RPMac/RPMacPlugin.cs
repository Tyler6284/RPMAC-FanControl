using System;
using System.Collections.Generic;

namespace FanControl.RPMac
{
    internal sealed class RpmacFanSensor : FanControl.Plugins.IPluginSensor
    {
        private readonly int _index;
        private readonly Func<int, double> _read;
        public RpmacFanSensor(int index, string name, Func<int, double> read)
        {
            _index = index;
            _read = read;
            Id = "RPMac/Fan/" + index + "/RPM";
            Name = name;
        }
        public string Id { get; private set; }
        public string Name { get; private set; }
        public System.Nullable<float> Value { get; private set; }
        public void Update()
        {
            try
            {
                double v = _read(_index);
                Value = double.IsNaN(v) ? (float?)null : (float)v;
            }
            catch { Value = null; }
        }
    }

    internal sealed class RpmacTempSensor : FanControl.Plugins.IPluginSensor
    {
        private readonly string _key;
        private readonly Func<string, double> _read;
        public RpmacTempSensor(string key, string displayName, Func<string, double> read)
        {
            _key = key;
            _read = read;
            Id = "RPMac/Temp/" + key;
            Name = displayName;
        }
        public string Id { get; private set; }
        public string Name { get; private set; }
        public System.Nullable<float> Value { get; private set; }
        public string Key { get { return _key; } }
        public void Update()
        {
            try
            {
                double v = _read(_key);
                if (double.IsNaN(v) || v < -5 || v > 150) Value = null;
                else Value = (float)v;
            }
            catch { Value = null; }
        }
    }

    internal sealed class RpmacControlSensor : FanControl.Plugins.IPluginControlSensor2
    {
        private readonly int _index;
        private readonly Func<int, double> _min;
        private readonly Func<int, double> _max;
        private readonly Action<int, double> _setRpm;
        private readonly Action<int> _setAuto;

        public RpmacControlSensor(int index, string name, string pairedFanId,
            Func<int, double> min, Func<int, double> max,
            Action<int, double> setRpm, Action<int> setAuto)
        {
            _index = index;
            PairedFanSensorId = pairedFanId;
            _min = min; _max = max; _setRpm = setRpm; _setAuto = setAuto;
            Id = "RPMac/Fan/" + index + "/Control";
            Name = name;
        }

        public string Id { get; private set; }
        public string Name { get; private set; }
        public System.Nullable<float> Value { get; private set; }
        public string PairedFanSensorId { get; private set; }

        public void Update() { }

        public void Set(float val)
        {
            try
            {
                if (val < 0) val = 0; if (val > 100) val = 100;
                double mn = _min(_index);
                double mx = _max(_index);
                if (double.IsNaN(mn) || double.IsNaN(mx) || mx <= mn) return;
                double rpm = mn + (mx - mn) * (val / 100.0);
                _setRpm(_index, rpm);
                Value = val;
            }
            catch { }
        }

        public void Reset()
        {
            try
            {
                _setAuto(_index);
                Value = null;
            }
            catch { }
        }
    }

    public sealed class RPMacPlugin : FanControl.Plugins.IPlugin2
    {
        private readonly FanControl.Plugins.IPluginLogger _logger;
        private readonly List<RpmacFanSensor> _fans = new List<RpmacFanSensor>();
        private readonly List<RpmacTempSensor> _temps = new List<RpmacTempSensor>();
        private readonly List<RpmacControlSensor> _controls = new List<RpmacControlSensor>();
        private readonly Dictionary<int, double> _fanMin = new Dictionary<int, double>();
        private readonly Dictionary<int, double> _fanMax = new Dictionary<int, double>();
        private readonly Dictionary<int, double> _fanActual = new Dictionary<int, double>();
        private readonly Dictionary<string, double> _tempVals = new Dictionary<string, double>();
        private bool _initialized;

        public RPMacPlugin(FanControl.Plugins.IPluginLogger logger) { _logger = logger; }
        public RPMacPlugin() { }

        public string Name { get { return "RPMac (Apple SMC)"; } }

        private void Log(string msg)
        {
            try { if (_logger != null) _logger.Log("[RPMac] " + msg); } catch { }
        }

        public void Initialize()
        {
            try
            {
                _initialized = false;
                _fans.Clear(); _temps.Clear(); _controls.Clear();
                _fanMin.Clear(); _fanMax.Clear(); _fanActual.Clear(); _tempVals.Clear();

                bool ok = global::RPMac.Smc.Validate();
                Log("SMC validate: " + ok + " | " + global::RPMac.Smc.SafetyReason + " | mmio=" + global::RPMac.Smc.MmioActive);

                int n = global::RPMac.Smc.FanCount();
                for (int i = 0; i < n; i++)
                {
                    int idx = i;
                    string smcName = null;
                    try { smcName = global::RPMac.Smc.FanName(idx); } catch { }
                    string fanLabel = string.IsNullOrEmpty(smcName)
                        ? ("RPMac Fan " + idx)
                        : ("RPMac " + smcName.Trim());
                    var fan = new RpmacFanSensor(idx, fanLabel + " RPM", j => {
                        double v; return _fanActual.TryGetValue(j, out v) ? v : double.NaN;
                    });
                    _fans.Add(fan);

                    if (global::RPMac.Smc.WritesAllowed)
                    {
                        var ctrl = new RpmacControlSensor(idx, fanLabel + " Control", fan.Id,
                            j => { double v; return _fanMin.TryGetValue(j, out v) ? v : double.NaN; },
                            j => { double v; return _fanMax.TryGetValue(j, out v) ? v : double.NaN; },
                            (j, rpm) => global::RPMac.Smc.SetFanRpm(j, rpm),
                            j => global::RPMac.Smc.SetFanAuto(j));
                        _controls.Add(ctrl);
                    }
                }

                try
                {
                    var keys = global::RPMac.Smc.EnumTempKeys();
                    if (keys != null)
                    {
                        foreach (var k in keys)
                        {
                            string key = k;
                            string name = key;
                            try { name = global::RPMac.Smc.TempName(key); } catch { name = key; }
                            if (string.IsNullOrEmpty(name)) name = key;
                            string display = (name != key) ? ("RPMac " + name + " (" + key + ")") : ("RPMac " + key);
                            _temps.Add(new RpmacTempSensor(key, display, kk => {
                                double v; return _tempVals.TryGetValue(kk, out v) ? v : double.NaN;
                            }));
                        }
                    }
                }
                catch (Exception ex) { Log("Temp enum failed: " + ex.Message); }

                // Prime initial values so sensors aren't null on first frame
                RefreshCaches();
                foreach (var f in _fans) f.Update();
                foreach (var t in _temps) t.Update();

                _initialized = true;
            }
            catch (Exception ex)
            {
                Log("Initialize error: " + ex);
                _initialized = false;
            }
        }

        public void Load(FanControl.Plugins.IPluginSensorsContainer _container)
        {
            try
            {
                if (_container == null) return;
                foreach (var t in _temps) _container.TempSensors.Add(t);
                foreach (var f in _fans) _container.FanSensors.Add(f);
                foreach (var c in _controls) _container.ControlSensors.Add(c);
                Log("Load: temps=" + _temps.Count + " fans=" + _fans.Count + " controls=" + _controls.Count
                    + " writes=" + global::RPMac.Smc.WritesAllowed);
            }
            catch (Exception ex)
            {
                Log("Load error: " + ex);
            }
        }

        private void RefreshCaches()
        {
            try
            {
                var infos = global::RPMac.Smc.GetFans();
                if (infos != null)
                {
                    foreach (var fi in infos)
                    {
                        _fanActual[fi.Index] = fi.Actual;
                        _fanMin[fi.Index] = fi.Min;
                        _fanMax[fi.Index] = fi.Max;
                    }
                }
            }
            catch { }

            try
            {
                for (int i = 0; i < _temps.Count; i++)
                {
                    string k = _temps[i].Key;
                    _tempVals[k] = global::RPMac.Smc.ReadTemp(k);
                }
            }
            catch { }
        }

        public void Update()
        {
            if (!_initialized) return;
            RefreshCaches();
            try { foreach (var f in _fans) f.Update(); } catch { }
            try { foreach (var t in _temps) t.Update(); } catch { }
        }

        public void Close()
        {
            try
            {
                foreach (var c in _controls) { try { c.Reset(); } catch { } }
            }
            catch { }
            try { global::RPMac.Smc.Cleanup(); } catch { }
            _initialized = false;
        }
    }
}
