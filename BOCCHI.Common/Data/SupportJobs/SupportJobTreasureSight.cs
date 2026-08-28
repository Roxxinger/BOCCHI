using BOCCHI.Common.Data.OccultCrescent;
using BOCCHI.Common.Services;

namespace BOCCHI.Common.Data.SupportJobs;

public static class SupportJobTreasureSight
{
    /// <summary>Freelancer level for Occult Treasuresight (action slot II, unlock level 10).</summary>
    public static byte RequiredFreelancerLevel => PhantomActions.TreasuresightUnlockLevel;

    public static bool CanCast(ISupportJobFactory supportJobs) =>
        supportJobs.Create(SupportJobId.PhantomFreelancer).Level >= RequiredFreelancerLevel;
}
