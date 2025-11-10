using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;

namespace Haley.Utils {

    public partial class AdapterGateway {
        public Task<IFeedback<List<Dictionary<string, object>>>> ReadAsync(string key, string query, params (string key, object value)[] parameters) {
            return ReadAsync(new AdapterArgs(key) { Query = query }, parameters);
        }
        public Task<IFeedback<Dictionary<string, object>>> ReadSingleAsync(string key, string query, params (string key, object value)[] parameters) {
            return ReadSingleAsync(new AdapterArgs(key) { Query = query, Filter = ResultFilter.FirstDictionary }, parameters);
        }
        public Task<IFeedback<T>> ScalarAsync<T>(string key, string query, params (string key, object value)[] parameters) {
            return ScalarAsync<T>(new AdapterArgs(key) { Query = query }, parameters);
        }
        public Task<IFeedback<bool>> NonQueryAsync(string key, string query, params (string key, object value)[] parameters) {
            return NonQueryAsync(new AdapterArgs(key) { Query = query }, parameters);
        }

        public async Task<IFeedback<List<Dictionary<string, object>>>> ReadAsync(IAdapterArgs input, params (string key, object value)[] parameters) {
            var fb = new Feedback<List<Dictionary<string, object>>>();
            try {
                var result = await Read(input, parameters);
                if (result is not List<Dictionary<string, object>> list || list.Count == 0) return fb.SetMessage("No records found.");
                return fb.SetStatus(true).SetResult(list);
            } catch (System.Exception ex) {
                if (ThrowCRUDExceptions) throw;
                return fb.SetTrace(ex.Message);
            }
        }

        public async Task<IFeedback<Dictionary<string, object>>> ReadSingleAsync(IAdapterArgs input, params (string key, object value)[] parameters) {
            var fb = new Feedback<Dictionary<string, object>>();
            try {
                if (input is AdapterArgs ex) ex.Filter = ResultFilter.FirstDictionary;
                var result = await Read(input, parameters);
                if (result is not Dictionary<string, object> dic) return fb.SetMessage("Record not found.");
                return fb.SetStatus(true).SetResult(dic);
            } catch (System.Exception ex) {
                if (ThrowCRUDExceptions) throw;
                return fb.SetTrace(ex.Message);
            }
        }

        public async Task<IFeedback<T>> ScalarAsync<T>(IAdapterArgs input, params (string key, object value)[] parameters) {
            var fb = new Feedback<T>();
            try {
                var result = await Scalar(input, parameters);
                if (result == null) return fb.SetMessage("No result returned.");

                // bool first (covers BIT(1), TINYINT(1), string, byte[])
                if (typeof(T) == typeof(bool)) {
                    if (TryToBool(result, out var bv)) return fb.SetStatus(true).SetResult((T)(object)bv);
                    return fb.SetMessage($"Unexpected scalar type for bool. Got {result.GetType().Name} value '{result}'.");
                }

                // Fast-path numeric conversions commonly used (int/long)
                if (typeof(T) == typeof(long) && long.TryParse(result.ToString(), out var l))
                    return fb.SetStatus(true).SetResult((T)(object)l);
                if (typeof(T) == typeof(int) && int.TryParse(result.ToString(), out var i))
                    return fb.SetStatus(true).SetResult((T)(object)i);

                if (result is T typed) return fb.SetStatus(true).SetResult(typed);
                return fb.SetMessage($"Unexpected scalar type. Expected {typeof(T).Name}, got {result.GetType().Name}.");
            } catch (System.Exception ex) {
                if (ThrowCRUDExceptions) throw;
                return fb.SetTrace(ex.Message);
            }
        }

        public async Task<IFeedback<bool>> NonQueryAsync(IAdapterArgs input, params (string key, object value)[] parameters) {
            var fb = new Feedback<bool>();
            try {
                await NonQuery(input, parameters);
                return fb.SetStatus(true).SetResult(true);
            } catch (System.Exception ex) {
                if (ThrowCRUDExceptions) throw;
                return fb.SetTrace(ex.Message);
            }
        }
        static bool TryToBool(object value, out bool b) {
            switch (value) {
                case bool vb: b = vb; return true;
                case byte by: b = by != 0; return true;                 // TINYINT(1)
                case sbyte sby: b = sby != 0; return true;
                case short sh: b = sh != 0; return true;
                case ushort ush: b = ush != 0; return true;
                case int vi: b = vi != 0; return true;
                case uint u: b = u != 0; return true;
                case long vl: b = vl != 0; return true;
                case ulong ul: b = ul != 0; return true;
                case decimal dec: b = dec != 0m; return true;
                case double d: b = d != 0d; return true;
                case float f: b = f != 0f; return true;
                case byte[] arr when arr.Length > 0: b = arr[0] != 0; return true; // BIT(1) as bytes
                case string s:
                if (bool.TryParse(s, out var bp)) { b = bp; return true; }
                if (long.TryParse(s, out var ln)) { b = ln != 0; return true; }
                break;
            }
            b = default;
            return false;
        }
    }
}