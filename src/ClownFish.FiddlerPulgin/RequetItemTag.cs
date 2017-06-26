using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClownFish.FiddlerPulgin
{
    internal sealed class RequetItemTag
    {
        /// <summary>
        /// 请求显示文本
        /// </summary>
        public string Request { get; set; }

        // Fiddler中的会话标记ID
        public string SessionId { get; set; }
    }
}
