namespace Autodesk.Revit.DB
{
    public partial class ElementId
    {
        public int IntegerValue { get; }

        public ElementId(int value)
        {
            IntegerValue = value;
        }
    }
}
