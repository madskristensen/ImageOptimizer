using System.ComponentModel;

namespace MadsKristensen.ImageOptimizer
{
    public class General : BaseOptionModel<General>, IRatingConfig
    {
        [Browsable(false)]
        public int RatingRequests { get; set; }
    }
}
