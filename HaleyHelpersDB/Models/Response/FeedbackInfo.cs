using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Models {
    public class FeedbackInfo : Feedback {
        public FeedbackInfo(string message) :base(true,message){  }
    }
}
