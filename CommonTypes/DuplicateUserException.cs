using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace CommonTypes
{

    [Serializable]
    public class DuplicateUserException : ApplicationException
    {
        private String name;

        public String getName()
        {
            return this.name;
        }

        public DuplicateUserException(String name)
        {
            this.name = name;
        }

        public DuplicateUserException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            name = info.GetString("name");
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("name", name);
        }
    }
}
