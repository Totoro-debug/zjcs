// Selected original payment type identities.
using System.ComponentModel;
namespace Zjcs.Official.BagItemQueries
{
public enum PayType
{
	[Description("周期卡")]
	PeriodicCard = 0,
	[Description("直充")]
	DirectPay = 2,
	[Description("战令")]
	BattlePass = 3,
	[Description("弹出式限时礼包")]
	LimitedTimeGift = 4,
	[Description("代金券")]
	Voucher = 5,
	[Description("活动限购礼包")]
	ActivityGift = 6,
	[Description("活跃付费活动礼包")]
	ActivePay = 7,
	[Description("每日礼包")]
	DailyGift = 8,
	[Description("每周礼包")]
	WeeklyGift = 9,
	[Description("首充礼包")]
	FirstPayGift = 10,
	[Description("幻装礼包")]
	AppearanceGift = 11,
	[Description("成长基金")]
	ActivityGrowthFund = 12,
	[Description("预购活动")]
	ActivityPrePay = 13,
	[Description("通用活动商店")]
	GeneralActivityShop = 14,
	[Description("活动限时礼包")]
	ActivityLimitedTimeGift = 15,
	[Description("回归活动商店")]
	ActivityComebackShop = 16,
	[Description("网页三方充值代金券")]
	WebPayVoucher = 100,
	[Description("网页三方充值礼包")]
	WebPayGift = 101,
	[Description("抖音商城")]
	DouyinPay = 102,
	[Description("平台优惠码礼包")]
	RedeemCodeGift = 200,
	[Description("平台优惠码代金券")]
	RedeemCodeVoucher = 201
}
}
