using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Client.Data.BMD
{
    public class SkillBMDReader : BaseReader<Dictionary<int, SkillBMD>>
    {
        private const int MaxSkills = 1024;

        /// <summary>
        /// Size of <c>SKILL_ATTRIBUTE_FIELDS</c> after the name in MuMain/OpenMU skill BMD (same for legacy and current file format).
        /// Legacy file: 32-byte name + 56 = 88 bytes/record. Season 20+ file: 50-byte <c>Name</c> + 56 = 106 bytes/record.
        /// </summary>
        private const int BodyAfterNameBytes = 56;

        /// <summary>MuMain <see cref="SKILL_ATTRIBUTE_FILE_LEGACY"/> — 32-byte UTF-8 name.</summary>
        private const int RecordSizeLegacy = 32 + BodyAfterNameBytes;

        /// <summary>MuMain <see cref="SKILL_ATTRIBUTE_FILE"/> — <c>MAX_SKILL_NAME</c> (50) byte UTF-8 name (e.g. MU 1.20.x Full pack).</summary>
        private const int RecordSizeSeason20 = 50 + BodyAfterNameBytes;

        private const ushort ChecksumKey = 0x5A18;

        private readonly byte[] BuxCode = { 0xFC, 0xCF, 0xAB };

        private void DecryptRecord(ReadOnlySpan<byte> encrypted, Span<byte> destination)
        {
            for (var i = 0; i < encrypted.Length; i++)
            {
                destination[i] = (byte)(encrypted[i] ^ BuxCode[i % BuxCode.Length]);
            }
        }

        private SkillBMD ParseSkill(ReadOnlySpan<byte> record, int nameLength)
        {
            var skill = new SkillBMD();

            if (record.Length < nameLength + BodyAfterNameBytes)
                throw new InvalidDataException($"Skill record too small: {record.Length} for nameLen={nameLength}.");

            var nameSpan = record.Slice(0, nameLength);
            var nullIndex = nameSpan.IndexOf((byte)0);
            if (nullIndex < 0)
                nullIndex = nameLength;

            skill.Name = Encoding.UTF8.GetString(nameSpan.Slice(0, nullIndex)).TrimEnd();

            var offset = nameLength;

            skill.RequiredLevel = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(offset, sizeof(ushort)));
            offset += sizeof(ushort);

            skill.Damage = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(offset, sizeof(ushort)));
            offset += sizeof(ushort);

            skill.ManaCost = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(offset, sizeof(ushort)));
            offset += sizeof(ushort);

            skill.AbilityGaugeCost = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(offset, sizeof(ushort)));
            offset += sizeof(ushort);

            skill.Distance = BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(offset, sizeof(uint)));
            offset += sizeof(uint);

            skill.Delay = BinaryPrimitives.ReadInt32LittleEndian(record.Slice(offset, sizeof(int)));
            offset += sizeof(int);

            skill.RequiredEnergy = BinaryPrimitives.ReadInt32LittleEndian(record.Slice(offset, sizeof(int)));
            offset += sizeof(int);

            skill.RequiredLeadership = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(offset, sizeof(ushort)));
            offset += sizeof(ushort);

            skill.MasteryType = record[offset++];
            skill.SkillUseType = record[offset++];

            skill.SkillBrand = BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(offset, sizeof(uint)));
            offset += sizeof(uint);

            skill.KillCount = record[offset++];

            record.Slice(offset, SkillBMD.MaxDutyClass).CopyTo(skill.RequireDutyClass);
            offset += SkillBMD.MaxDutyClass;

            record.Slice(offset, SkillBMD.MaxClass).CopyTo(skill.RequireClass);
            offset += SkillBMD.MaxClass;

            skill.SkillRank = record[offset++];

            skill.MagicIcon = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(offset, sizeof(ushort)));
            offset += sizeof(ushort);

            var typeValue = record[offset++];
            skill.Type = typeValue <= (byte)TypeSkill.FriendlySkill
                ? (TypeSkill)typeValue
                : TypeSkill.None;

            offset++; // padding byte before Strength

            skill.RequiredStrength = BinaryPrimitives.ReadInt32LittleEndian(record.Slice(offset, sizeof(int)));
            offset += sizeof(int);

            skill.RequiredDexterity = BinaryPrimitives.ReadInt32LittleEndian(record.Slice(offset, sizeof(int)));
            offset += sizeof(int);

            skill.ItemSkill = record[offset++];
            skill.IsDamage = record[offset++] != 0;
            skill.Effect = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(offset, sizeof(ushort)));
            offset += sizeof(ushort);

            // Optional trailing padding in some MSVC builds — ignore if present.
            return skill;
        }

        private uint GenerateChecksum(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < sizeof(uint))
            {
                return 0;
            }

            var key = (uint)ChecksumKey;
            var result = key << 9;

            for (var offset = 0; offset <= buffer.Length - sizeof(uint); offset += sizeof(uint))
            {
                var value = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(offset, sizeof(uint)));
                var index = (offset / sizeof(uint)) + ChecksumKey;

                if ((index & 1) == 0)
                {
                    result ^= value;
                }
                else
                {
                    result += value;
                }

                if ((offset % 16) == 0)
                {
                    result ^= (uint)((key + result) >> (((offset / sizeof(uint)) % 8) + 1));
                }
            }

            return result;
        }

        protected override Dictionary<int, SkillBMD> Read(byte[] buffer)
        {
            if (buffer.Length < RecordSizeLegacy)
                throw new InvalidDataException($"Skill buffer is too small ({buffer.Length} bytes).");

            // Match MuMain SkillDataLoader: trailing DWORD checksum after MaxSkills records (same total sizes as OpenMU/MuMain).
            int recordSize;
            ReadOnlySpan<byte> dataSpan;

            if (buffer.Length >= sizeof(uint) && (buffer.Length - sizeof(uint)) % MaxSkills == 0)
            {
                recordSize = (buffer.Length - sizeof(uint)) / MaxSkills;
                dataSpan = buffer.AsSpan(0, recordSize * MaxSkills);

                var storedChecksum = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(recordSize * MaxSkills, sizeof(uint)));
                var computedChecksum = GenerateChecksum(dataSpan);

                if (storedChecksum != 0 && storedChecksum != computedChecksum)
                {
                    throw new InvalidDataException(
                        $"Skill file checksum mismatch. stored=0x{storedChecksum:X8}, computed=0x{computedChecksum:X8}");
                }
            }
            else if (buffer.Length % MaxSkills == 0)
            {
                recordSize = buffer.Length / MaxSkills;
                dataSpan = buffer.AsSpan();
            }
            else
            {
                throw new InvalidDataException(
                    $"Unexpected skill.bmd size ({buffer.Length} bytes). Expected (N×{MaxSkills}) or (N×{MaxSkills}+4) with checksum.");
            }

            // MuMain uses exactly sizeof(SKILL_ATTRIBUTE_FILE_LEGACY)==88 or sizeof(SKILL_ATTRIBUTE_FILE)==106 (packed).
            int nameLength = recordSize switch
            {
                RecordSizeLegacy => 32,
                RecordSizeSeason20 => 50,
                _ => throw new InvalidDataException(
                    $"Unsupported skill record size {recordSize}. Expected legacy {RecordSizeLegacy} or Season20/OpenMU-style {RecordSizeSeason20}.")
            };

            var skills = new Dictionary<int, SkillBMD>();
            var recordCount = Math.Min(MaxSkills, dataSpan.Length / recordSize);
            Span<byte> decrypted = stackalloc byte[RecordSizeSeason20]; // max supported record size

            for (var index = 0; index < recordCount; index++)
            {
                var recordSpan = dataSpan.Slice(index * recordSize, recordSize);
                DecryptRecord(recordSpan, decrypted.Slice(0, recordSize));

                var skill = ParseSkill(decrypted.Slice(0, recordSize), nameLength);
                if (!string.IsNullOrWhiteSpace(skill.Name))
                    skills[index] = skill;
            }

            return skills;
        }
    }
}
