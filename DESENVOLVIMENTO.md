# Underdog 3D — checkpoints de desenvolvimento

## Como retomar

Leia este arquivo antes de implementar. Consulte git status e git log; preserve trabalho local. Nao refaca checkpoints concluidos. Nao execute Prepare Survival Game para aplicar apenas IA: ele recria a partida. Camera, arena, HUD, habilidades, vida/dano e WaveManager estao fora do escopo desta etapa.

## CP0 — baseline e analise (2026-11-15)

Estado inicial: branch mage, HEAD d966bbb. A sobrevivencia aprovada ainda estava em arquivos locais modificados/novos: runtime, assets gerados em Assets/Generated/UnderdogSurvival, cena UnderdogSurvival, pacotes, Build Settings e relatorios anteriores. Sera criado um commit local de baseline com esses arquivos antes dos commits de IA; nenhum descarte ou push.

Analise concluida:
- EnemyAI decide perseguir/atacar por distancia e reenvia o destino do jogador a cada 0.2 s. Ranged apenas usa um ponto atras de si para recuar.
- Todos os agentes partilham destino e prioridade padrao; nao ha reserva de posicoes, separacao local ou identidade de flanco. Isso favorece filas e congestionamento.
- Agent usa updatePosition=false e updateRotation=false; CharacterController.Move aplica desiredVelocity e gravidade; agent.nextPosition e sincronizado ao fim do frame. Preservar essa divisao.
- ArenaNavigation apenas carrega/remove NavMeshData; WaveManager controla as cinco ondas e spawns. Nenhum dos dois precisa mudar.
- Prefabs gerados: Melee (100 HP/2.5), Fast (60 HP/4), Ranged (75 HP/2.6), avoidance HighQuality. Os novos valores solicitados serao aplicados por comando seguro de Editor aos prefabs referenciados pelo manager, sem regravar cena ou ondas.

Proximas etapas:
1. CP1: destinos validos distribuidos, separacao local e atualizacoes escalonadas; preservar ataques/animacoes e validar compilacao.
2. CP2: flanqueamento e ranged com histerese/linha de tiro; comando seguro de presets e compilacao.
3. CP3: testes automatizados de navegacao/grupos/combate, regressao das ondas inalteradas, instrucoes e commit final.

Testes pendentes: perseguicao individual, melee em grupo, flanco, ranged em paredes/cobertura, grupo misto, dash do jogador e 15–25 agentes. Compilar nao significa validar gameplay ou visual.
