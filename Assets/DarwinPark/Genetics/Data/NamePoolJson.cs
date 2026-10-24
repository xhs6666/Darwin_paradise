using System;
using System.Collections.Generic;

namespace DarwinPark.Genetics
{
    [Serializable]
    public class NamePoolJson
    {
        public List<string> familyNames = new List<string>();
        public List<string> givenNames = new List<string>();
    }
}