using System;

namespace Autodesk.Revit.DB
{
    public partial class Element : IDisposable
    {
        public ElementId Id { get; }

        public ElementConfiguration Configure { get; } = new();

        public Element(ElementId id)
        {
            Id = id;
        }

        public Element(int id)
            : this(new ElementId(id))
        {
        }

        public virtual Parameter GetParameter(Guid guid)
        {
            return Configure.GetParameter?.Invoke(guid)
                ?? throw new InvalidOperationException("GetParameter not configured.");
        }

        public virtual void Dispose()
        {
            Configure.Dispose?.Invoke();
        }
    }

    public partial class ElementConfiguration
    {
        public Func<Guid, Parameter>? GetParameter { get; set; }
        public Action? Dispose { get; set; }
    }
}
