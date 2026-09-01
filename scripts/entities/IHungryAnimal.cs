using Godot;

namespace HiepSiVeVuon.Entities
{
    // Cow/Sheep/Pig/Horse/Chicken/Goat khong co lop cha chung (moi con la 1 CharacterBody3D doc
    // lap, xem ghi chu trong tung file) nen khong the duyet chung polymorphic - interface nho nay
    // CHI de NPC (FarmhandNpc/StablehandNpc/ScheduledFarmNpc...) doc IsHungry/HungerDays cua BAT
    // KY con vat nao trong pham vi minh phu trach ma khong can biet no la loai gi cu the.
    public interface IHungryAnimal
    {
        bool IsHungry { get; }
        int HungerDays { get; }
        Vector3 GlobalPosition { get; }
    }
}
