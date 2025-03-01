using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haley.Models {
    public class FeedbackError : Feedback {
        public FeedbackError(string message) : base(false,message) { }
    }
}
