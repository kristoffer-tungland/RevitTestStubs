using System;

namespace Autodesk.Revit.DB
{
    public partial class Parameter : Element
    {
        public new ParameterConfiguration Configure { get; } = new();

        public Parameter(ElementId id) : base(id) { }

        public Parameter(int id) : base(id) { }

        public Guid GUID
        {
            get
            {
                return Configure.get_Guid?.Invoke()
                    ?? throw new InvalidOperationException("get_Guid not configured.");
            }
        }

        public override void Dispose()
        {
            Configure.Dispose?.Invoke();
        }
    }

    public partial class ParameterConfiguration : ElementConfiguration
    {
        public Func<Guid>? get_Guid { get; set; }
    }
}
