using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Telegram.Models
{
    public abstract class Serializable
    {
        public string ToJson() => JsonConvert.SerializeObject(this);

        public override string ToString() => ToJson();
    }
}
