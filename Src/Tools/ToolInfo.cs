using System;
using System.Linq;
using System.Reflection;
using RT.Util;
using RT.Util.ExtensionMethods;
using RT.Util.Forms;

namespace MeshEdit
{
    sealed class ToolInfo
    {
        public MethodInfo Method { get; private set; }
        public ToolAttribute Attribute { get; private set; }
        public ToolParamAttribute[] Parameters { get; private set; }

        private ToolInfo() { }

        public static ToolInfo CreateFromMethod(MethodInfo method)
        {
            var attr = method.GetCustomAttribute<ToolAttribute>();
            if (attr == null)
                return null;
            var prms = method.GetParameters().Select(p => p.GetCustomAttribute<ToolParamAttribute>()).ToArray();
            return new ToolInfo { Method = method, Attribute = attr, Parameters = prms };
        }

        public override string ToString()
        {
            return $"{Attribute.ReadableName}{(Parameters.Length == 0 ? null : $" (+ {Parameters.Length})")}";
        }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    sealed class ToolAttribute : Attribute
    {
        public ToolAttribute(string readableName) { ReadableName = readableName; }
        public string ReadableName { get; private set; }
    }

    abstract class ToolParamAttribute : Attribute
    {
        public string Prompt { get; private set; }
        public ToolParamAttribute(string prompt) { Prompt = prompt; }
        public abstract bool AskForValue(string parameterName, ref object value);
    }

    sealed class ToolBoolAttribute : ToolParamAttribute
    {
        public ToolBoolAttribute(string prompt, string readableFalseName, string readableTrueName) : base(prompt)
        {
            ReadableFalseName = readableFalseName;
            ReadableTrueName = readableTrueName;
        }
        public string ReadableFalseName { get; private set; }
        public string ReadableTrueName { get; private set; }

        public override bool AskForValue(string parameterName, ref object value)
        {
            var result = DlgMessage.Show(Prompt, parameterName, DlgType.Question, ReadableFalseName, ReadableTrueName, "Cancel");
            value = result == 0 ? false : result == 1 ? true : (object) null;
            return result < 2;
        }
    }

    sealed class ToolEnumAttribute : ToolParamAttribute
    {
        public ToolEnumAttribute(string prompt, Type enumType, params string[] readableNames) : base(prompt)
        {
            ReadableNames = readableNames;
            EnumType = enumType;
        }
        public Type EnumType { get; private set; }
        public string[] ReadableNames { get; private set; }

        public override bool AskForValue(string parameterName, ref object value)
        {
            var result = DlgMessage.Show(Prompt, parameterName, DlgType.Question, ReadableNames.Concat("Cancel").ToArray());
            value = result == ReadableNames.Length ? null : Enum.ToObject(EnumType, result);
            return value != null;
        }
    }

    abstract class ToolInputBoxAttribute : ToolParamAttribute
    {
        public ToolInputBoxAttribute(string prompt) : base(prompt) { }

        public override bool AskForValue(string parameterName, ref object value)
        {
            tryAgain:
            var result = InputBox.GetLine(Prompt, @default: value?.ToString() ?? "", caption: "Parameter");
            if (result == null)
                return false;
            if (!tryParse(result, ref value))
            {
                var result2 = DlgMessage.Show("The value is not valid. Do you want to try again?", "Error", DlgType.Error, "&Yes", "&No");
                if (result2 == 0)
                    goto tryAgain;
                return false;
            }
            return true;
        }

        protected abstract bool tryParse(string input, ref object value);
    }

    sealed class ToolDoubleAttribute : ToolInputBoxAttribute
    {
        public ToolDoubleAttribute(string prompt) : base(prompt) { }

        protected override bool tryParse(string input, ref object value)
        {
            double dbl;
            var ret = double.TryParse(input, out dbl);
            if (ret)
                value = dbl;
            return ret;
        }
    }

    sealed class ToolIntAttribute : ToolInputBoxAttribute
    {
        public ToolIntAttribute(string prompt) : base(prompt) { }

        protected override bool tryParse(string input, ref object value)
        {
            int integer;
            var ret = int.TryParse(input, out integer);
            if (ret)
                value = integer;
            return ret;
        }
    }
}
