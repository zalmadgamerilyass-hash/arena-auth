import 'dotenv/config';
import {
    Client,
    GatewayIntentBits,
    SlashCommandBuilder,
    REST,
    Routes,
    EmbedBuilder,
    PermissionFlagsBits
} from 'discord.js';

const TOKEN = process.env.DISCORD_TOKEN;
const CLIENT_ID = process.env.CLIENT_ID;
const GUILD_ID = process.env.GUILD_ID;
const API_URL = (process.env.API_BASE_URL || 'http://localhost:5000/api/bot').replace(/\/+$/, '');
const BOT_SECRET = process.env.BOT_SECRET || 'zyrex-bot-9f4c2e7a1b8d6f3e5c0a7b4d2f8e1c6a';
const ADMIN_ROLE_ID = process.env.ADMIN_ROLE_ID;

// Colors
const COLOR_PRIMARY = 0x8B5CF6; // Purple
const COLOR_SUCCESS = 0x10B981; // Green
const COLOR_ERROR = 0xEF4444;   // Red
const COLOR_WARNING = 0xF59E0B; // Orange

// Helper for API requests
async function callApi(endpoint, method = 'GET', body = null) {
    const url = `${API_URL}${endpoint.startsWith('/') ? endpoint : '/' + endpoint}`;
    const headers = {
        'x-bot-secret': BOT_SECRET,
        'Content-Type': 'application/json'
    };

    const options = {
        method,
        headers
    };

    if (body) {
        options.body = JSON.stringify(body);
    }

    try {
        const res = await fetch(url, options);
        const data = await res.json().catch(() => null);
        return { ok: res.ok, status: res.status, data };
    } catch (err) {
        console.error(`[API Error] ${method} ${url}:`, err.message);
        return { ok: false, status: 0, error: err.message };
    }
}

// Check admin authorization
function isUserAuthorized(interaction) {
    if (interaction.memberPermissions?.has(PermissionFlagsBits.Administrator)) {
        return true;
    }
    if (ADMIN_ROLE_ID && interaction.member?.roles?.cache?.has(ADMIN_ROLE_ID)) {
        return true;
    }
    return false;
}

// Define Slash Commands
const commands = [
    new SlashCommandBuilder()
        .setName('genkey')
        .setDescription('🔑 Generate new license key(s) for Arena / PowerX Auth')
        .setDefaultMemberPermissions(PermissionFlagsBits.Administrator)
        .addIntegerOption(opt =>
            opt.setName('days')
                .setDescription('Duration in days (e.g. 1, 7, 30, 365, or 99999 for Lifetime)')
                .setRequired(false)
                .setMinValue(1)
        )
        .addIntegerOption(opt =>
            opt.setName('count')
                .setDescription('Number of keys to generate (1 - 25, default: 1)')
                .setRequired(false)
                .setMinValue(1)
                .setMaxValue(25)
        )
        .addUserOption(opt =>
            opt.setName('user')
                .setDescription('Tag a user to assign and DM the key directly')
                .setRequired(false)
        )
        .addStringOption(opt =>
            opt.setName('note')
                .setDescription('Custom note or customer reference for this key')
                .setRequired(false)
        )
        .addBooleanOption(opt =>
            opt.setName('public')
                .setDescription('Send visibly in this channel instead of private (default: private)')
                .setRequired(false)
        ),

    new SlashCommandBuilder()
        .setName('checkkey')
        .setDescription('🔍 Check information and status of a license key')
        .setDefaultMemberPermissions(PermissionFlagsBits.Administrator)
        .addStringOption(opt =>
            opt.setName('key')
                .setDescription('The license key to inspect')
                .setRequired(true)
        ),

    new SlashCommandBuilder()
        .setName('resethwid')
        .setDescription('🔄 Reset HWID lock for a license key / user')
        .setDefaultMemberPermissions(PermissionFlagsBits.Administrator)
        .addStringOption(opt =>
            opt.setName('key')
                .setDescription('The license key whose HWID should be cleared')
                .setRequired(true)
        ),

    new SlashCommandBuilder()
        .setName('extendkey')
        .setDescription('⏳ Extend validity days of an existing key')
        .setDefaultMemberPermissions(PermissionFlagsBits.Administrator)
        .addStringOption(opt =>
            opt.setName('key')
                .setDescription('The license key to extend')
                .setRequired(true)
        )
        .addIntegerOption(opt =>
            opt.setName('days')
                .setDescription('Extra days to add')
                .setRequired(true)
                .setMinValue(1)
        ),

    new SlashCommandBuilder()
        .setName('delkey')
        .setDescription('🗑️ Delete a license key permanently')
        .setDefaultMemberPermissions(PermissionFlagsBits.Administrator)
        .addStringOption(opt =>
            opt.setName('key')
                .setDescription('The license key to delete')
                .setRequired(true)
        ),

    new SlashCommandBuilder()
        .setName('stats')
        .setDescription('📊 View Auth Server statistics (total keys, apps, users)')
        .setDefaultMemberPermissions(PermissionFlagsBits.Administrator),

    new SlashCommandBuilder()
        .setName('help')
        .setDescription('📖 Show all available bot commands and setup guide')
];

// Initialize Discord Client
const client = new Client({
    intents: [
        GatewayIntentBits.Guilds,
        GatewayIntentBits.GuildMembers
    ]
});

// Register Slash Commands
async function registerCommands() {
    if (!TOKEN || !CLIENT_ID) {
        console.warn('⚠️ [Discord] DISCORD_TOKEN or CLIENT_ID is missing in .env!');
        console.warn('Please fill in .env to register slash commands.');
        return;
    }

    const rest = new REST({ version: '10' }).setToken(TOKEN);

    try {
        console.log('🔄 Registering slash commands...');
        const commandJson = commands.map(c => c.toJSON());

        if (GUILD_ID && GUILD_ID.trim().length > 0) {
            // Guild-specific (instant update)
            await rest.put(
                Routes.applicationGuildCommands(CLIENT_ID, GUILD_ID.trim()),
                { body: commandJson }
            );
            console.log(`✅ Successfully registered commands for Guild ID: ${GUILD_ID}`);
        } else {
            // Global commands
            await rest.put(
                Routes.applicationCommands(CLIENT_ID),
                { body: commandJson }
            );
            console.log('✅ Successfully registered global commands (may take a few minutes to cache).');
        }
    } catch (error) {
        console.error('❌ Failed to register slash commands:', error);
    }
}

client.once('ready', async () => {
    console.log('========================================================');
    console.log(`🤖 Bot is online as ${client.user.tag}`);
    console.log(`🔗 Connecting to Auth Server at: ${API_URL}`);
    console.log('========================================================');
    await registerCommands();
});

// Handle Interactions
client.on('interactionCreate', async (interaction) => {
    if (!interaction.isChatInputCommand()) return;

    // Check Permissions
    if (interaction.commandName !== 'help' && !isUserAuthorized(interaction)) {
        return interaction.reply({
            embeds: [
                new EmbedBuilder()
                    .setTitle('❌ Permission Denied')
                    .setDescription('You do not have permission to execute this administrative command.')
                    .setColor(COLOR_ERROR)
            ],
            ephemeral: true
        });
    }

    const { commandName } = interaction;

    // COMMAND: /genkey
    if (commandName === 'genkey') {
        const days = interaction.options.getInteger('days') ?? 30;
        const count = interaction.options.getInteger('count') ?? 1;
        const targetUser = interaction.options.getUser('user');
        const customNote = interaction.options.getString('note');
        const isPublic = interaction.options.getBoolean('public') ?? false;

        await interaction.deferReply({ ephemeral: !isPublic });

        const reqBody = {
            count: count,
            durationDays: days,
            ownerDiscordId: targetUser ? targetUser.id : interaction.user.id,
            ownerDiscordName: targetUser ? targetUser.tag : interaction.user.tag
        };

        const res = await callApi('/keys/generate', 'POST', reqBody);

        if (!res.ok || !res.data?.keys) {
            return interaction.editReply({
                embeds: [
                    new EmbedBuilder()
                        .setTitle('❌ Key Generation Failed')
                        .setDescription(`Could not connect to Auth API or request failed.\n\`Status: ${res.status}\``)
                        .setColor(COLOR_ERROR)
                ]
            });
        }

        const keys = res.data.keys;
        const keyListFormatted = keys.map(k => `\`\`\`${k.key}\`\`\``).join('');

        const embed = new EmbedBuilder()
            .setTitle('🔑 License Key(s) Generated')
            .setColor(COLOR_SUCCESS)
            .addFields(
                { name: 'Keys Count', value: `${keys.length}`, inline: true },
                { name: 'Duration', value: days >= 99999 ? '♾️ Lifetime' : `${days} Days`, inline: true },
                { name: 'Assigned User', value: targetUser ? `<@${targetUser.id}>` : `<@${interaction.user.id}>`, inline: true },
                { name: 'License Key(s)', value: keyListFormatted }
            )
            .setFooter({ text: 'Arena / PowerX Auth Security System' })
            .setTimestamp();

        if (customNote) {
            embed.addFields({ name: 'Note', value: customNote });
        }

        // If target user provided, try to send them a private DM as well
        if (targetUser && targetUser.id !== interaction.user.id) {
            try {
                const dmEmbed = new EmbedBuilder()
                    .setTitle('🎁 You Received a License Key!')
                    .setDescription(`Here is your license key for **Arena / PowerX** provided by **${interaction.user.tag}**:`)
                    .addFields(
                        { name: 'Duration', value: days >= 99999 ? '♾️ Lifetime' : `${days} Days`, inline: true },
                        { name: 'Your Key', value: keyListFormatted }
                    )
                    .setColor(COLOR_PRIMARY)
                    .setFooter({ text: 'Do not share your key with anyone!' })
                    .setTimestamp();

                await targetUser.send({ embeds: [dmEmbed] });
                embed.addFields({ name: 'Direct Message', value: `✅ Successfully DM'd <@${targetUser.id}>` });
            } catch (dmErr) {
                embed.addFields({ name: 'Direct Message', value: `⚠️ Could not DM <@${targetUser.id}> (DMs closed)` });
            }
        }

        return interaction.editReply({ embeds: [embed] });
    }

    // COMMAND: /checkkey
    if (commandName === 'checkkey') {
        const key = interaction.options.getString('key').trim();
        await interaction.deferReply({ ephemeral: true });

        const res = await callApi(`/keys/${encodeURIComponent(key)}`, 'GET');

        if (!res.ok || !res.data?.key) {
            return interaction.editReply({
                embeds: [
                    new EmbedBuilder()
                        .setTitle('🔍 Key Not Found')
                        .setDescription(`The key \`${key}\` does not exist in the database.`)
                        .setColor(COLOR_ERROR)
                ]
            });
        }

        const k = res.data.key;
        const embed = new EmbedBuilder()
            .setTitle(`🔎 Key Details: \`${k.key}\``)
            .setColor(k.status === 'available' ? COLOR_SUCCESS : COLOR_WARNING)
            .addFields(
                { name: 'Status', value: `\`${k.status.toUpperCase()}\``, inline: true },
                { name: 'Duration', value: k.durationDays >= 99999 ? 'Lifetime' : `${k.durationDays} Days`, inline: true },
                { name: 'App ID', value: `\`${k.appId || 'Default'}\``, inline: true },
                { name: 'Created', value: k.created ? `<t:${Math.floor(new Date(k.created).getTime() / 1000)}:R>` : 'Unknown', inline: true },
                { name: 'Last Used', value: k.lastUsed ? `<t:${Math.floor(new Date(k.lastUsed).getTime() / 1000)}:R>` : 'Never', inline: true }
            )
            .setFooter({ text: 'Arena / PowerX Auth Security System' })
            .setTimestamp();

        return interaction.editReply({ embeds: [embed] });
    }

    // COMMAND: /resethwid
    if (commandName === 'resethwid') {
        const key = interaction.options.getString('key').trim();
        await interaction.deferReply({ ephemeral: true });

        const res = await callApi(`/keys/${encodeURIComponent(key)}/reset-hwid`, 'POST');

        if (!res.ok) {
            return interaction.editReply({
                embeds: [
                    new EmbedBuilder()
                        .setTitle('❌ HWID Reset Failed')
                        .setDescription(`Failed to reset HWID for key \`${key}\`.`)
                        .setColor(COLOR_ERROR)
                ]
            });
        }

        return interaction.editReply({
            embeds: [
                new EmbedBuilder()
                    .setTitle('🔄 HWID Reset Success')
                    .setDescription(`The hardware lock (HWID) for \`${key}\` has been cleared. The user can now log in on a new device.`)
                    .setColor(COLOR_SUCCESS)
                    .setTimestamp()
            ]
        });
    }

    // COMMAND: /extendkey
    if (commandName === 'extendkey') {
        const key = interaction.options.getString('key').trim();
        const days = interaction.options.getInteger('days');
        await interaction.deferReply({ ephemeral: true });

        const res = await callApi(`/keys/${encodeURIComponent(key)}/extend`, 'POST', { extraDays: days });

        if (!res.ok) {
            return interaction.editReply({
                embeds: [
                    new EmbedBuilder()
                        .setTitle('❌ Extension Failed')
                        .setDescription(`Failed to extend key \`${key}\`. Make sure the key exists.`)
                        .setColor(COLOR_ERROR)
                ]
            });
        }

        return interaction.editReply({
            embeds: [
                new EmbedBuilder()
                    .setTitle('⏳ Key Extended')
                    .setDescription(`Key \`${key}\` extended by **+${days} days**!\nNew total duration: **${res.data?.durationDays} days**.`)
                    .setColor(COLOR_SUCCESS)
                    .setTimestamp()
            ]
        });
    }

    // COMMAND: /delkey
    if (commandName === 'delkey') {
        const key = interaction.options.getString('key').trim();
        await interaction.deferReply({ ephemeral: true });

        const res = await callApi(`/keys/${encodeURIComponent(key)}`, 'DELETE');

        if (!res.ok) {
            return interaction.editReply({
                embeds: [
                    new EmbedBuilder()
                        .setTitle('❌ Deletion Failed')
                        .setDescription(`Could not delete key \`${key}\`.`)
                        .setColor(COLOR_ERROR)
                ]
            });
        }

        return interaction.editReply({
            embeds: [
                new EmbedBuilder()
                    .setTitle('🗑️ Key Deleted')
                    .setDescription(`Key \`${key}\` has been permanently deleted from the database.`)
                    .setColor(COLOR_SUCCESS)
                    .setTimestamp()
            ]
        });
    }

    // COMMAND: /stats
    if (commandName === 'stats') {
        await interaction.deferReply({ ephemeral: true });

        const res = await callApi('/stats', 'GET');

        if (!res.ok || !res.data?.stats) {
            return interaction.editReply({
                embeds: [
                    new EmbedBuilder()
                        .setTitle('❌ Stats Failed')
                        .setDescription('Could not fetch stats from Auth Server.')
                        .setColor(COLOR_ERROR)
                ]
            });
        }

        const s = res.data.stats;
        const embed = new EmbedBuilder()
            .setTitle('📊 Arena / PowerX Auth Statistics')
            .setColor(COLOR_PRIMARY)
            .addFields(
                { name: '🔑 Total Keys', value: `\`${s.total}\``, inline: true },
                { name: '👥 Total Users', value: `\`${s.users}\``, inline: true },
                { name: '📱 Total Applications', value: `\`${s.apps}\``, inline: true }
            )
            .setFooter({ text: 'Auth Server Online' })
            .setTimestamp();

        return interaction.editReply({ embeds: [embed] });
    }

    // COMMAND: /help
    if (commandName === 'help') {
        const helpEmbed = new EmbedBuilder()
            .setTitle('📖 Arena / PowerX Auth Bot Commands')
            .setColor(COLOR_PRIMARY)
            .setDescription('Manage your license keys and authentication system directly from Discord.')
            .addFields(
                { name: '`/genkey [days] [count] [user] [note]`', value: 'Generate one or multiple license keys. Option to DM key directly to a user.' },
                { name: '`/checkkey [key]`', value: 'Check key status, expiration, and last used time.' },
                { name: '`/resethwid [key]`', value: 'Reset HWID lock for a user or key.' },
                { name: '`/extendkey [key] [days]`', value: 'Add extra days to an existing key.' },
                { name: '`/delkey [key]`', value: 'Delete a key from the database.' },
                { name: '`/stats`', value: 'View total keys, users, and apps statistics.' }
            )
            .setFooter({ text: 'Developed for Arena System Auth' })
            .setTimestamp();

        return interaction.reply({ embeds: [helpEmbed], ephemeral: true });
    }
});

// Start Bot
if (!TOKEN) {
    console.error('❌ DISCORD_TOKEN is missing in DiscordBot/.env! Please add it.');
} else {
    client.login(TOKEN).catch(err => {
        console.error('❌ Failed to login to Discord:', err.message);
    });
}
