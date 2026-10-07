using System.Globalization;

namespace CourtDesk
{
    public static class ArabicCulture
    {
        private static CultureInfo _value;

        public static CultureInfo Value
        {
            get
            {
                if (_value == null)
                {
                    var ar = new CultureInfo("ar-EG");
                    ar.NumberFormat.DigitSubstitution = DigitShapes.None;
                    _value = ar;
                }
                return _value;
            }
        }
    }
}
