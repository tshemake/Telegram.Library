using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram
{
    public interface IConfiguration
    {
        int ApiId { get; }
        string ApiHash { get; }
        string PhoneNumber { get; }
        string PhoneCodeHash { get; set; }
        string ServerHost { get; }
        int ServerPort { get; }
    }
}
