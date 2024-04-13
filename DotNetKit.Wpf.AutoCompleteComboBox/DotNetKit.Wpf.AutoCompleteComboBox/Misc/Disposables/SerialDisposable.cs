using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNetKit.Misc.Disposables
{
    sealed class SerialDisposable
        : IDisposable
    {
        IDisposable content;

        public IDisposable Content
        {
            get { return content; }
            set
            {
                if (content != null)
                {
                    content.Dispose();
                }

                content = value;
            }
        }

        public void Dispose()
        {
            Content = null;
        }
    }
}
