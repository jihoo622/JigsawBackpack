using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Tests.Relayout
{
    internal static class R
    {
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static object Call(object _target, string _method, params object[] _args)
        {
            var methods = _target.GetType().GetMethods(AnyInstance)
                .Where(_m => _m.Name == _method && _m.GetParameters().Length == (_args?.Length ?? 0))
                .ToList();
            if (methods.Count == 0)
            {
                throw new MissingMethodException(_target.GetType().FullName, _method);
            }
            MethodInfo match = methods[0];
            if (methods.Count > 1 && _args != null)
            {
                foreach (var m in methods)
                {
                    var ps = m.GetParameters();
                    bool ok = true;
                    for (int i = 0; i < ps.Length; i++)
                    {
                        if (_args[i] != null && !ps[i].ParameterType.IsInstanceOfType(_args[i]))
                        {
                            ok = false;
                            break;
                        }
                    }
                    if (ok)
                    {
                        match = m;
                        break;
                    }
                }
            }
            return match.Invoke(_target, _args);
        }

        public static T Call<T>(object _target, string _method, params object[] _args)
        {
            return (T)Call(_target, _method, _args);
        }

        public static bool CallOutString(object _target, string _method, object _arg0, out string _outStr)
        {
            var args = new object[] { _arg0, null };
            var methods = _target.GetType().GetMethods(AnyInstance)
                .Where(_m => _m.Name == _method && _m.GetParameters().Length == 2)
                .ToList();
            MethodInfo match = methods.First(_m =>
            {
                var p0 = _m.GetParameters()[0].ParameterType;
                return _arg0 == null || p0.IsInstanceOfType(_arg0);
            });
            bool result = (bool)match.Invoke(_target, args);
            _outStr = (string)args[1];
            return result;
        }

        public static object Get(object _target, string _member)
        {
            var t = _target.GetType();
            var prop = t.GetProperty(_member, AnyInstance);
            if (prop != null) return prop.GetValue(_target);
            var field = t.GetField(_member, AnyInstance);
            if (field != null) return field.GetValue(_target);
            throw new MissingMemberException(t.FullName, _member);
        }

        public static T Get<T>(object _target, string _member) => (T)Get(_target, _member);

        public static void Set(object _target, string _member, object _value)
        {
            var t = _target.GetType();
            var prop = t.GetProperty(_member, AnyInstance);
            if (prop != null) { prop.SetValue(_target, _value); return; }
            var field = t.GetField(_member, AnyInstance);
            if (field != null) { field.SetValue(_target, _value); return; }
            throw new MissingMemberException(t.FullName, _member);
        }

        public static List<object> List(object _target, string _member)
        {
            var seq = (IEnumerable)Get(_target, _member);
            return seq.Cast<object>().ToList();
        }
    }
}
