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
const ADMIN_ROLE_ID = process.env.ADMIN_ROLE_ID;

// KeyAuth Configuration
const KEYAUTH_SELLER_KEY = process.env.KEYAUTH_SELLER_KEY;
const KEYAUTH_KEY_PREFIX = process.env.KEYAUTH_KEY_PREFIX || 'POWERX';

// Local Server Configuration
const API_URL = (process.env.API_BASE_URL || 'http://localhost:5000/api/bot').replace(/\/+$/, '');
const BOT_SECRET = process.env.BOT_SECRET || 'zyrex-bot-9f4c2e7a1b8d6f3e5c0a7b4d2f8e1c6a';

// Colors
const COLOR_PRIMARY = 0x8B5CF6; // Purple
const COLOR_SUCCESS = 0x10B981; // Green
const COLOR_ERROR = 0xEF4444;   // Red
const COLOR_WARNING = 0xF59E0B; // Orange

const isKeyAuthMode = Boolean(KEYAUTH_SELLER_KEY && KEYAUTH_SELLER_KEY.trim().length > 0);

console.log('========================================================');
if (isKeyAuthMode) {
    console.log('🌐 Mode: KEYAUTH CLOUD API (keyauth.win)');
    console.log(`🔑 Key Prefix: ${KEYAUTH_KEY_PREFIX}`);
} else {
    console.log('💻 Mode: LOCAL AUTH SERVER');
    console.log(`🔗 Target URL: ${API_URL}`);
}
console.log('========================================================');

// Helper for KeyAuth Seller API
async function callKeyAuth(params) {
    const query = new URLSearchParams({
        sellerkey: KEYAUTH_SELLER_KEY.trim(),
        format: 'json',
        ...params
    });

    const url = `https://keyauth.win/api/seller/?${query.toString()}`;

    try {
        const res = await fetch(url);
        const data = await res.json().catch(() => null);
        if (!data) {
            return { ok: false, message: 'Invalid response from KeyAuth API' };
        }
        return { ok: Boolean(data.success), data, message: data.message };
    } catch (err) {
        console.error('[KeyAuth API Error]:', err.message);
        return { ok: false, message: err.message };
    }
}

// Helper for Local API requests
async function callLocalApi(endpoint, method = 'GET', body = null) {
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
        console.error(`[Local API Error] ${method} ${url}:`, err.message);
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
        .setDescription('🔑 Generate new license key(s) for POWER X Auth')
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
        .setDescription('📊 View Auth statistics (keys, users, etc.)')
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
        return;
    }

    const rest = new REST({ version: '10' }).setToken(TOKEN);

    try {
        console.log('🔄 Registering slash commands...');
        const commandJson = commands.map(c => c.toJSON());

        if (GUILD_ID && GUILD_ID.trim().length > 0) {
            await rest.put(
                Routes.applicationGuildCommands(CLIENT_ID, GUILD_ID.trim()),
                { body: commandJson }
            );
            console.log(`✅ Successfully registered commands for Guild: ${GUILD_ID}`);
        } else {
            await rest.put(
                Routes.applicationCommands(CLIENT_ID),
                { body: commandJson }
            );
            console.log('✅ Successfully registered global commands.');
        }
    } catch (error) {
        console.error('❌ Failed to register slash commands:', error);
    }
}

client.once('ready', async () => {
    console.log('========================================================');
    console.log(`🤖 Bot is online as ${client.user.tag}`);
    console.log('========================================================');
    await registerCommands();
});

// Handle Interactions
client.on('interactionCreate', async (interaction) => {
    if (!interaction.isChatInputCommand()) return;

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

    // ==========================================
    // COMMAND: /genkey
    // ==========================================
    if (commandName === 'genkey') {
        const days = interaction.options.getInteger('days') ?? 30;
        const count = interaction.options.getInteger('count') ?? 1;
        const targetUser = interaction.options.getUser('user');
        const customNote = interaction.options.getString('note');
        const isPublic = interaction.options.getBoolean('public') ?? false;

        await interaction.deferReply({ ephemeral: !isPublic });

        let generatedKeys = [];

        if (isKeyAuthMode) {
            // KeyAuth API mode
            const mask = `${KEYAUTH_KEY_PREFIX}-******-******-******`;
            const noteText = customNote
                ? customNote
                : targetUser
                    ? `User: ${targetUser.tag} (${targetUser.id})`
                    : `By: ${interaction.user.tag}`;

            const res = await callKeyAuth({
                type: 'add',
                expiry: days,
                mask: mask,
                amount: count,
                level: 1,
                note: noteText
            });

            if (!res.ok) {
                return interaction.editReply({
                    embeds: [
                        new EmbedBuilder()
                            .setTitle('❌ Key Generation Failed (KeyAuth)')
                            .setDescription(`KeyAuth returned an error:\n\`\`\`${res.message || 'Unknown error'}\`\`\`\n*Make sure your KEYAUTH_SELLER_KEY is correct in .env*`)
                            .setColor(COLOR_ERROR)
                    ]
                });
            }

            if (res.data?.key) {
                generatedKeys = [res.data.key];
            } else if (Array.isArray(res.data?.keys)) {
                generatedKeys = res.data.keys;
            } else if (typeof res.data?.message === 'string' && res.data.message.includes(KEYAUTH_KEY_PREFIX)) {
                generatedKeys = [res.data.message.trim()];
            } else {
                generatedKeys = ['KEY-GENERATED-CHECK-KEYAUTH-PANEL'];
            }
        } else {
            // Local Server mode
            const reqBody = {
                count: count,
                durationDays: days,
                ownerDiscordId: targetUser ? targetUser.id : interaction.user.id,
                ownerDiscordName: targetUser ? targetUser.tag : interaction.user.tag
            };

            const res = await callLocalApi('/keys/generate', 'POST', reqBody);

            if (!res.ok || !res.data?.keys) {
                return interaction.editReply({
                    embeds: [
                        new EmbedBuilder()
                            .setTitle('❌ Key Generation Failed')
                            .setDescription(`Could not connect to Auth API on \`${API_URL}\`.\n\n**Possible causes:**\n• If you are using **KeyAuth.cc**, please add your \`KEYAUTH_SELLER_KEY\` in \`DiscordBot/.env\`!\n• If you are running the local server, make sure \`start-server.bat\` is running.\n\n\`Status: ${res.status}\``)
                            .setColor(COLOR_ERROR)
                    ]
                });
            }

            generatedKeys = res.data.keys.map(k => k.key);
        }

        const keyListFormatted = generatedKeys.map(k => `\`\`\`${k}\`\`\``).join('');

        const embed = new EmbedBuilder()
            .setTitle('🔑 License Key(s) Generated')
            .setColor(COLOR_SUCCESS)
            .addFields(
                { name: 'Keys Count', value: `${generatedKeys.length}`, inline: true },
                { name: 'Duration', value: days >= 99999 ? '♾️ Lifetime' : `${days} Days`, inline: true },
                { name: 'Provider', value: isKeyAuthMode ? '🌐 KeyAuth Cloud' : '💻 Local Auth', inline: true },
                { name: 'Assigned User', value: targetUser ? `<@${targetUser.id}>` : `<@${interaction.user.id}>`, inline: true },
                { name: 'License Key(s)', value: keyListFormatted }
            )
            .setFooter({ text: 'POWER X Security Engine' })
            .setTimestamp();

        if (customNote) {
            embed.addFields({ name: 'Note', value: customNote });
        }

        if (targetUser && targetUser.id !== interaction.user.id) {
            try {
                const dmEmbed = new EmbedBuilder()
                    .setTitle('🎁 You Received a License Key!')
                    .setDescription(`Here is your license key for **POWER X** provided by **${interaction.user.tag}**:`)
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

    // ==========================================
    // COMMAND: /checkkey
    // ==========================================
    if (commandName === 'checkkey') {
        const key = interaction.options.getString('key').trim();
        await interaction.deferReply({ ephemeral: true });

        if (isKeyAuthMode) {
            const res = await callKeyAuth({ type: 'info', key });
            if (!res.ok) {
                return interaction.editReply({
                    embeds: [
                        new EmbedBuilder()
                            .setTitle('🔍 Key Not Found')
                            .setDescription(`KeyAuth: \`${res.message || 'Key does not exist'}\``)
                            .setColor(COLOR_ERROR)
                    ]
                });
            }

            const info = res.data;
            const embed = new EmbedBuilder()
                .setTitle(`🔎 Key Details: \`${key}\``)
                .setColor(COLOR_SUCCESS)
                .addFields(
                    { name: 'Status', value: `\`${info.status || (info.usedby ? 'USED' : 'UNUSED')}\``, inline: true },
                    { name: 'Used By', value: info.usedby ? `\`${info.usedby}\`` : 'None', inline: true },
                    { name: 'Duration', value: `${info.expiry || 'N/A'} Days`, inline: true },
                    { name: 'HWID', value: info.hwid ? `\`${info.hwid.slice(0, 16)}...\`` : 'Not locked', inline: true }
                )
                .setFooter({ text: 'KeyAuth Cloud' })
                .setTimestamp();

            return interaction.editReply({ embeds: [embed] });
        } else {
            const res = await callLocalApi(`/keys/${encodeURIComponent(key)}`, 'GET');

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
                    { name: 'App ID', value: `\`${k.appId || 'Default'}\``, inline: true }
                )
                .setFooter({ text: 'POWER X Local Auth' })
                .setTimestamp();

            return interaction.editReply({ embeds: [embed] });
        }
    }

    // ==========================================
    // COMMAND: /resethwid
    // ==========================================
    if (commandName === 'resethwid') {
        const key = interaction.options.getString('key').trim();
        await interaction.deferReply({ ephemeral: true });

        if (isKeyAuthMode) {
            const res = await callKeyAuth({ type: 'resetkey', key });
            if (!res.ok) {
                return interaction.editReply({
                    embeds: [
                        new EmbedBuilder()
                            .setTitle('❌ HWID Reset Failed')
                            .setDescription(`KeyAuth: \`${res.message || 'Failed to reset key'}\``)
                            .setColor(COLOR_ERROR)
                    ]
                });
            }

            return interaction.editReply({
                embeds: [
                    new EmbedBuilder()
                        .setTitle('🔄 HWID Reset Success')
                        .setDescription(`The HWID lock for key \`${key}\` has been reset on KeyAuth.`)
                        .setColor(COLOR_SUCCESS)
                        .setTimestamp()
                ]
            });
        } else {
            const res = await callLocalApi(`/keys/${encodeURIComponent(key)}/reset-hwid`, 'POST');

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
    }

    // ==========================================
    // COMMAND: /extendkey
    // ==========================================
    if (commandName === 'extendkey') {
        const key = interaction.options.getString('key').trim();
        const days = interaction.options.getInteger('days');
        await interaction.deferReply({ ephemeral: true });

        if (isKeyAuthMode) {
            const res = await callKeyAuth({ type: 'extend', key, expiry: days });
            if (!res.ok) {
                return interaction.editReply({
                    embeds: [
                        new EmbedBuilder()
                            .setTitle('❌ Extension Failed')
                            .setDescription(`KeyAuth: \`${res.message || 'Failed to extend key'}\``)
                            .setColor(COLOR_ERROR)
                    ]
                });
            }

            return interaction.editReply({
                embeds: [
                    new EmbedBuilder()
                        .setTitle('⏳ Key Extended')
                        .setDescription(`Key \`${key}\` extended by **+${days} days** on KeyAuth!`)
                        .setColor(COLOR_SUCCESS)
                        .setTimestamp()
                ]
            });
        } else {
            const res = await callLocalApi(`/keys/${encodeURIComponent(key)}/extend`, 'POST', { extraDays: days });

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
    }

    // ==========================================
    // COMMAND: /delkey
    // ==========================================
    if (commandName === 'delkey') {
        const key = interaction.options.getString('key').trim();
        await interaction.deferReply({ ephemeral: true });

        if (isKeyAuthMode) {
            const res = await callKeyAuth({ type: 'del', key });
            if (!res.ok) {
                return interaction.editReply({
                    embeds: [
                        new EmbedBuilder()
                            .setTitle('❌ Deletion Failed')
                            .setDescription(`KeyAuth: \`${res.message || 'Could not delete key'}\``)
                            .setColor(COLOR_ERROR)
                    ]
                });
            }

            return interaction.editReply({
                embeds: [
                    new EmbedBuilder()
                        .setTitle('🗑️ Key Deleted')
                        .setDescription(`Key \`${key}\` has been permanently deleted from KeyAuth.`)
                        .setColor(COLOR_SUCCESS)
                        .setTimestamp()
                ]
            });
        } else {
            const res = await callLocalApi(`/keys/${encodeURIComponent(key)}`, 'DELETE');

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
    }

    // ==========================================
    // COMMAND: /stats
    // ==========================================
    if (commandName === 'stats') {
        await interaction.deferReply({ ephemeral: true });

        if (isKeyAuthMode) {
            const res = await callKeyAuth({ type: 'stats' });
            if (!res.ok) {
                return interaction.editReply({
                    embeds: [
                        new EmbedBuilder()
                            .setTitle('❌ Stats Failed')
                            .setDescription(`KeyAuth: \`${res.message || 'Could not fetch stats'}\``)
                            .setColor(COLOR_ERROR)
                    ]
                });
            }

            const s = res.data;
            const embed = new EmbedBuilder()
                .setTitle('📊 KeyAuth Statistics')
                .setColor(COLOR_PRIMARY)
                .addFields(
                    { name: '🔑 Total Keys', value: `\`${s.total_keys || s.keys || 'N/A'}\``, inline: true },
                    { name: '👥 Total Users', value: `\`${s.total_users || s.users || 'N/A'}\``, inline: true },
                    { name: '📱 Total App Users', value: `\`${s.app_users || 'N/A'}\``, inline: true }
                )
                .setFooter({ text: 'POWER X KeyAuth' })
                .setTimestamp();

            return interaction.editReply({ embeds: [embed] });
        } else {
            const res = await callLocalApi('/stats', 'GET');

            if (!res.ok || !res.data?.stats) {
                return interaction.editReply({
                    embeds: [
                        new EmbedBuilder()
                            .setTitle('❌ Stats Failed')
                            .setDescription('Could not fetch stats from local Auth Server.')
                            .setColor(COLOR_ERROR)
                    ]
                });
            }

            const s = res.data.stats;
            const embed = new EmbedBuilder()
                .setTitle('📊 Local Auth Statistics')
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
    }

    // ==========================================
    // COMMAND: /help
    // ==========================================
    if (commandName === 'help') {
        const modeText = isKeyAuthMode ? '🌐 Mode: **KeyAuth Cloud**' : '💻 Mode: **Local Auth Server**';
        const helpEmbed = new EmbedBuilder()
            .setTitle('📖 POWER X Auth Bot Commands')
            .setColor(COLOR_PRIMARY)
            .setDescription(`${modeText}\nManage your license keys and authentication system directly from Discord.`)
            .addFields(
                { name: '`/genkey [days] [count] [user] [note]`', value: 'Generate license keys. Sends DM directly to tagged user.' },
                { name: '`/checkkey [key]`', value: 'Check key status, expiration, and HWID.' },
                { name: '`/resethwid [key]`', value: 'Reset HWID lock for a key.' },
                { name: '`/extendkey [key] [days]`', value: 'Add extra days to an existing key.' },
                { name: '`/delkey [key]`', value: 'Delete a key permanently.' },
                { name: '`/stats`', value: 'View total keys, users, and app stats.' }
            )
            .setFooter({ text: 'POWER X Auth Security System' })
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
