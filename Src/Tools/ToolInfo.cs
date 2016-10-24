using System;
using System.Linq;
using System.Reflection;
using RT.Util;
using RT.Util.Dialogs;
using RT.Util.ExtensionMethods;

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
        public abstract bool AskForValue(out object value);
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

        public override bool AskForValue(out object value)
        {
            var result = DlgMessage.ShowQuestion(Prompt, ReadableFalseName, ReadableTrueName, "Cancel");
            value = result == 0 ? false : result == 1 ? true : (object) null;
            return result < 2;
        }
    }
}
