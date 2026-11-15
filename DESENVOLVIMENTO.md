# Underdog 3D — checkpoints de desenvolvimento

## Como retomar

Leia este arquivo antes de implementar, comecando pelo ultimo checkpoint (CP9). Consulte git status e git log; preserve trabalho local e nao refaca checkpoints concluidos. Nao execute Prepare Survival Game para atualizar sistemas isolados: ele recria a partida. Para a expansao atual use Prepare Combat Expansion conforme Docs/CombatExpansion.md; camera, arena, composicao das cinco ondas e HUD existente devem permanecer preservados.

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

## CP1 — runtime de posicionamento (em validacao)

Baseline confirmado: 4011aaf (sobrevivencia aprovada e CP0 ja registrados; nao houve necessidade de duplicar o commit).
Arquivos em trabalho: EnemyAI.cs e EnemyNavigationChecks.cs (+ meta).
Implementado: destinos reservados em aneis, separacao local no mesmo grupo/alvo, prioridades de avoidance distintas, decisoes escalonadas com intervalo minimo, limite de caminhos por decisao, parametros do Animator em cache e velocidade medida do CharacterController. Ataques continuam por seus eventos/timers existentes e pausam a navegacao sem cancelar animacoes. Rogue usa waypoint lateral antes de entrar no anel; Mage usa faixa ideal, histerese e procura posicoes com LOS no NavMesh.
Compilacao passou contra Unity 2022.3.62f3. Avisos anteriores: NavigationStatic e ItemEffect.
Falta antes de concluir CP1: suite real de Play Mode (individual, grupos melee, flanco, ranged/bordas/cobertura, grupo misto 25, dash e morte). Ajustar somente falhas observadas. Nenhum arquivo de ondas, arena, camera ou HUD foi alterado.
Depois: CP2 presets seguros para os prefabs existentes e ajuste somente da taxa da locomocao nos Controllers gerados; CP3 regressao/relatorios/instrucoes.
Validacao parcial CP1: individuo, oito melee, flanco, faixa ranged e recuo na borda passaram no runner Unity. Cenário com parede/LOS falhou; diagnostico de posicao/rota adicionado, investigacao pendente. Corrigida tambem inicializacao nativa de NavMeshPath para Start (Unity nao permite no inicializador de MonoBehaviour). Grupo 25 ainda nao executado ate superar LOS. Nao marcar CP1 como concluido antes da suite completa.

### CP1 concluido — navegacao e comportamentos

Validacao final em Unity 2022.3.62f3, projeto isolado, runner PlayMode com passo simulado de 1/60 s: PASS. A falha parcial de deslocamento vinha da fixture a 5000 unidades com passos diminutos no batch headless; corrigida a fixture para 200 unidades e o passo do runner, sem mudar a taxa do jogo. Parede/LOS tambem passou. Corrigida a tolerancia final de chegada melee para nao estacionar fora do alcance de ataque.

Testes executados: guerreiro individual; alcance final e pausa durante golpe; evento duplicado sem dano extra; cooldown; alvo escapando do alcance; cancelamento por morte; oito melee com destinos distintos; flanco lateral e aproximacao final; ranged aproximando/recuando; recuo junto da borda; contorno de cobertura para obter LOS; 25 agentes mistos sem sobreposicao persistente e dentro do NavMesh; alvo deslocado como dash; morte liberando reservas e colliders. Grupo misto: 1000 decisoes e 265 caminhos em 14 s simulados (nao e benchmark de CPU).

Arquivos do checkpoint: Assets/Scripts/Enemy/EnemyAI.cs, Assets/Tests/Editor/EnemyNavigationChecks.cs (+ meta), Docs/Validation/EnemyAI-PlayMode.xml e este documento. Compilacao Roslyn contra assemblies da Unity passou; avisos anteriores permanecem. Cena, ondas, prefabs, animacoes, vida/dano, camera e HUD nao foram regravados.

Falta: CP2 comando de Editor para aplicar HP/velocidades/perfis nos tres prefabs referenciados pelo WaveManager e ajustar apenas locomocao; CP3 regressao, instrucoes e relatorios. Testes visuais na arena aprovada, Hit/Death com os modelos reais e sensacao de combate continuam pendentes; a suite usa atores sinteticos para isolar navegacao e dano/eventos.

## CP2 concluido — configuracao segura dos prefabs

CP1 registrado no commit local e4de539. Novos arquivos: Assets/Editor/EnemyAISetup.cs e Assets/Tests/Editor/EnemyAISetupChecks.cs (ambos com meta), Docs/Validation/EnemyAI-Presets-EditMode.xml. Modificado Assets/Editor/SurvivalGameSetup.cs somente nos valores iniciais dos inimigos e na taxa de locomocao para futuras geracoes.

Tools > Underdog > Update Enemy AI Presets resolve os tres prefabs reais do WaveManager da cena ativa; valida papeis/componentes/Controllers antes de escrever e pede confirmacao com os caminhos exatos. Cria backups dos prefabs e Controllers copiados em Assets/Generated/EnemyAI_<data>. Aplica Warrior 175 HP/2.5, Rogue 60 HP/5.5, Mage 75 HP/2.6 e distancias 6/7.5/9; configura separacao e decisoes. Somente o estado Locomotion passa a usar LocomotionRate. Clips, eventos, velocidades dos ataques, Hit/Death, dano, cooldown e referencias do manager continuam existentes. Em falha durante gravacao, restaura prefabs mantendo seus .meta/GUIDs. Nao altera cenas nem assets KayKit.

Validacao: compilacao de todos os scripts contra Unity 2022.3.62f3 PASS; runner EditMode PASS em prefabs sinteticos (perfis, backups, GUIDs mantidos, Controllers originais preservados, ataque/evento/Hit/Death preservados, rejeicao de prefab duplicado). Relatorio XML anexado. Nao aplicado ainda aos prefabs da partida real: executar o menu na cena UnderdogSurvival, fora do Play Mode. Nao executar Prepare Survival Game para aplicar somente IA.

Falta CP3: regressao de sobrevivencia/combate, verificacao dos hashes de arquivos protegidos, documentacao de parametros e testes de arena. Validacao visual e balanceamento dos modelos reais ainda precisam de Play Mode interativo.

## CP3 concluido — regressao e entrega (2026-11-15)

CP2 registrado no commit local 30e7d51. Arquivos desta etapa: Docs/EnemyAI.md (instrucao, parametros e roteiro manual), relatorios XML em Docs/Validation, EnemyAISetup.cs/EnemyAISetupChecks.cs (recusa de alteracoes nao salvas no prefab/Controller/Prefab Mode) e EnemyAI.cs (restaurada acentuacao original de comentarios/rotulos). Nao houve outra mudanca de gameplay nesta etapa.

Validacoes finais executadas:
- Compilacao de todos os scripts runtime/Editor contra assemblies Unity 2022.3.62f3: PASS; somente os dois avisos preexistentes.
- Runner Unity EditMode em projeto isolado: 3/3 PASS. Presets via AssetDatabase/PrefabUtility, preservacao de backup/GUID/Controller/eventos/Hit/Death, slots duplicados e assets com alteracoes nao salvas rejeitados; regressao de vida e PlayerCombatSelfCheck existente.
- Runner Unity PlayMode em projeto isolado: 2/2 PASS. Suite nova de navegacao com colisores/agentes reais e suite SurvivalChecks existente (projeteis 60/10 FPS, dano unico, area, cinco ondas vazias/vitoria/derrota). Grupo de 25: 1000 decisoes, 259 caminhos em 14 s simulados no ultimo run; nao e medicao de CPU/GPU.
- Comparacao SHA256 de 3700 arquivos protegidos: zero diferencas. Diff contra baseline 4011aaf tambem sem alteracao em cenas, assets gerados/originais, ondas, jogador, combate/vida, camera, HUD, pacotes ou ProjectSettings.
- git diff --check: PASS. Nenhum push ou merge realizado.

Para aplicar: abrir UnderdogSurvival fora do Play Mode, salvar trabalho pendente nos assets, Tools > Underdog > Update Enemy AI Presets e conferir/confirmar os tres caminhos exibidos. O menu faz os backups e aplica os perfis; a partida/cena real nao foi regravada por esta sessao. NAO executar Prepare Survival Game para esta atualizacao. Nao precisa rebake do NavMesh existente.

Pendentes exclusivamente no Editor interativo: validar sensacao/animacoes dos modelos reais, Mage junto de paredes/colunas reais, dash real do jogador no grupo, tres magias, cinco ondas completas com combate, derrota/reinicio e Profiler com 15-25 agentes. Roteiro e todos os parametros em Docs/EnemyAI.md. Nao declarar esses testes manuais concluidos com base na compilacao ou nas fixtures sinteticas. Mudanca de HP/velocidade pode exigir ajuste fino posterior sem alterar a composicao das ondas.

Estado de retomada: runtime, ferramenta segura e suites concluidos; nao refazer CP1/CP2. Proxima sessao deve aplicar/validar na arena e corrigir apenas problemas observados. O arquivo nao rastreado Assets/Generated/UnderdogSurvival/EditorBuildSettings.backup.txt.meta apareceu no import do Editor; foi preservado e ficou fora dos commits de IA. Commits locais de implementacao: e4de539 e 30e7d51; o commit deste checkpoint sucede ambos.

## CP4 concluido — protecao de feedback; hipotese de pausa nao confirmada (2026-11-15)

Usuario relatou que Iniciar/Enter nao respondiam e confirmou que o botao Pause da Unity estava azul. Com o Editor pausado, os frames de gameplay e callbacks de inicio nao avancam. Nao foi identificado defeito novo no binding do botao ou na progressao das ondas. O log do Editor tambem registra NullReferenceException em CombatFeedback.Restore/OnDisable apos recompilacoes; essa excecao pode acionar Error Pause. Nao foi possivel inspecionar ou alterar o estado vivo do Editor por Pipeline (indisponivel nesta Unity 2022.3).

Causa da excecao reproduzida: durante recarga de scripts, Renderer[] pode ser restaurado sem o cache nativo MaterialPropertyBlock[]; Restore verificava apenas renderers, mas acessava originais[i]. Agora recusa cache nulo/incompleto e entradas nulas, mantendo a restauracao normal quando o cache existe. Mudanca restrita a CombatFeedback.cs; nenhum ajuste em WaveManager, HUD, input, IA ou configuracao de ondas.

Arquivos: Assets/Scripts/Combat/CombatFeedback.cs; Assets/Tests/Editor/CombatFeedbackSelfCheck.cs (+ meta); Docs/Validation/StartResume-EditMode.xml; este checkpoint. Menu de verificacao: Tools > Underdog > Check Combat Feedback (Edit Mode).

Validacoes: regressao nova falhou antes da correcao com a mesma NullReferenceException da linha 48 (XML anterior guardado em Temp/StartResumeCheck/feedback-before.xml); apos correcao, runner Unity 2022.3 EditMode 4/4 PASS, incluindo vida, PlayerCombat e presets de IA existentes. Testa cache ausente, vazio, entradas nulas, Renderer removido e restauracao normal de propriedades. Compilacao de todos os scripts passou, com os dois avisos anteriores. Nao equivale a teste interativo de inicio da partida.

Alteracoes locais encontradas e preservadas: Melee/Fast/Ranged.prefab, UnderdogSurvival.unity (ajuste de fonte), nova pasta EnemyAI_20261115_143647 e seus metas. Nao inclui-las no commit desta correcao. Checkpoint anterior d667a11 permanece como baseline de IA.

Aplicacao/teste manual pendente: parar Play, aguardar compilacao, limpar erros antigos do Console, entrar novamente em Play e garantir que Pause nao esteja azul. Testar Iniciar, repetir com Enter e observar o comeco da primeira onda. Error Pause pode continuar habilitado; nao ocultar erros novos. Na proxima sessao, consultar este checkpoint e confirmar o teste no Editor antes de investigar outros sistemas.

## CP5 concluido — inicio validado com a cena real (2026-11-15)

Correcao do diagnostico: o usuario esclareceu que o botao reagia mas o painel continuava na tela; a conclusao anterior que atribuiu o bloqueio ao Pause foi insuficiente. CP4 comprovou/corrigiu somente a excecao independente do feedback, nao a causa desse relato de inicio. Nao assumir que testes sinteticos de ondas vazias validam o clique na cena real.

Foi preparada uma copia isolada da UnderdogSurvival atual com 129 assets/settings e dependencias reais (22.4 MiB), mantendo GUIDs, modelos, Controllers, configuracoes e pacotes locais, em Temp/StartSceneVerification. Teste Unity 2022.3.62f3 PlayMode carregou a cena, entregou evento de clique ao Button real, verificou mudanca Inicio -> Jogando, painel fechado, PlayerMovement/PlayerCombat habilitados e primeiro inimigo da primeira onda. PASS antes e depois de incorporar a checagem repetivel. XML final em Docs/Validation/ActualSceneStart-PlayMode.xml.

Na tentativa seguinte do proprio usuario, a captura temporaria no Editor registrou: playing=True, paused=False, frame=57, time=0.9488879, scale=1, NavMesh=490 vertices, WaveManager ativo em Jogando, painel inicial False, HP=100, movimento/combate ativos e um listener runtime no botao. O usuario confirmou: "agora desapareceu". Primeira onda agendada para time=4.698767 (intervalo inicial existente de 4 s). Portanto o inicio foi confirmado na sessao atual; nao foi identificado defeito reproduzivel adicional que justificasse alterar WaveManager, HUD, input ou ondas. Causa pontual do comportamento anterior permanece sem comprovacao; nao declarar uma nova correcao de gameplay.

Novo arquivo Assets/Tests/Editor/MatchStartChecks.cs (+ meta): Tools > Underdog > Check Match Start (Play Mode). Executar com a partida ainda na tela inicial e Play sem pausa. A checagem aciona o evento do botao existente e observa painel, comandos e primeiro spawn, sem alterar configuracao/quantidades. Nao simula o dispositivo fisico de mouse nem joga as cinco ondas completas. A captura temporaria StartGameProbe foi removida; nenhum monitor automatico foi deixado no projeto.

Validacao: compilacao PASS com os dois avisos preexistentes; teste da cena real 1/1 PASS; hashes dos quatro arquivos locais de cena/prefabs continuam iguais ao inicio da investigacao. Mudancas locais de fontes, presets e backups do usuario preservadas e fora do commit desta etapa. Nao houve push/merge nem alteracao de arquivos runtime de gameplay neste checkpoint.

Retomada: CP1-CP3 de IA e protecao de feedback CP4 continuam existentes. Se o inicio voltar a falhar, executar Check Match Start na tela inicial e capturar o primeiro erro/estado real antes de modificar sistemas. Pendente: confirmacao humana do primeiro spawn e jogabilidade completa apos o painel fechar; o primeiro spawn foi comprovado automaticamente na copia real.

## CP6 concluido — movimentacao, Stamina e religacao do inicio (2026-11-15)

Baseline local: 057513e. Preservadas as mudancas do usuario em UnderdogSurvival, tres prefabs de inimigos e pasta de presets. Analise: entrada Horizontal/Vertical correta no InputManager; CharacterController ativo, velocidade 5, root motion desligado. Na copia real, deslocamento direto de 2.5 unidades passou sem bloqueio por piso/Animator. Captura do Editor mostrou Inicio, HP100, movimento/combate desabilitados; nao capturou teclas WASD pressionadas. Portanto nao afirmar validacao de dispositivo fisico.

Falha reproduzida no HUD: apagar o listener runtime e reativar o componente deixava a partida em Inicio, pois a ligacao existia apenas em Start. Antes da correcao: 2/3 testes passavam, teste de religacao falhava (XML em Temp/PlayerMovementCheckpoint/hud-reload-before.xml). SurvivalHUD agora liga em OnEnable e remove somente seus callbacks em OnDisable. Sem mudanca de layout, spawns, ondas ou regras de jogo. MatchStartChecks passou a verificar essa religacao.

PlayerMovement ampliado preservando campos serializados e mecanicas: referencias iniciadas em Awake, aceleracao 40/frenagem55, Control com velocidade1.4x, Stamina100/consumo18/regeneracao25/atraso0.75/recuperacao minima20. Sem consumo parado ou bloqueado. Esgotado anda normalmente. Dash Shift sem custo, impulso12x0.2/cooldown1 e retorno curto com desaceleracao120; gravidade/colisao permanecem no CharacterController. Direcao de movimento independente da rotacao pelo mouse. Animator recebe deslocamento real; LocomotionRate opcional preparado para CP7. Morte impede atualizacao de movimento.

Arquivos do checkpoint: PlayerMovement.cs; SurvivalHUD.cs; MatchStartChecks.cs; PlayerMovementChecks.cs (+ meta); Docs/Validation/PlayerMovement-PlayMode.xml; este documento. Compilacao PASS (avisos anteriores apenas). Unity PlayMode na copia real: 3/3 PASS — inicio/spawn, religacao do botao, deslocamento real, Sprint durante evento LancarMagia e durante Hit; fixture testa aceleracao/frenagem, diagonal, consumo/atraso/regeneracao, esgotamento/recuperacao20, dash com/sem Stamina, cooldown, retorno andar/correr, parede em dt0.3, bloqueio sem consumo, Hit/morte. Entradas injetadas para repetir casos; nao simula teclado/mouse fisicos.

Pendentes CP7: ferramenta segura para ritmo do Animator existente, documentacao final e validacao de integridade. Testes manuais pendentes: dispositivo fisico em Game com foco, game feel, dash contra grupos/props reais, tres magias durante corrida e partida completa. Nao reimplementar CP1-CP6; WaveManager, camera, vida/dano e inimigos permanecem existentes.

## CP7 concluido — Animator, documentacao e entrega (2026-11-15)

CP6 registrado no commit local 478e44d. Novos arquivos: Assets/Editor/PlayerMovementSetup.cs (+ meta), Docs/PlayerMovement.md e Docs/Validation/PlayerMovement-Animator-EditMode.xml. Alterado SurvivalGameSetup.cs somente para configurar LocomotionRate no Animator do jogador de futuras geracoes. Nenhuma mudanca no gerador de ondas ou na camera.

Tools > Underdog > Update Player Locomotion identifica o Controller referenciado pelo PlayerMovement ativo, exige Edit Mode e assets salvos, recusa assets originais KayKit, pede confirmacao com caminho concreto, cria backup e parametriza somente Locomotion. Mantem GUID, Blend Tree, demais estados e clips/eventos; restaura o arquivo de backup em falha de gravacao. A cena nao e salva nem sobrescrita. Aplicacao automatica feita somente no Controller da copia de validacao, nao no asset da partida do usuario.

Clips reais reutilizados: Idle_A, Running_A, Walking_Backwards, Running_Strafe_Left/Right, Dodge_Forward/Backward/Left/Right (assets PlayerIdle/Forward/Backward/Left/Right, PlayerDash0-3 ja existentes). Sprint usa o ritmo da locomocao atual, sem inventar clip ou reconfigurar camadas Upper Body/Feedback.

Validacao final: compilacao de runtime/Editor PASS (avisos anteriores); Unity EditMode 1/1 PASS no Controller real copiado (backup/GUID/clips/eventos e Dash/ataques/Hit/Death preservados); Unity PlayMode 3/3 PASS na copia real com o Controller ajustado e as fixtures de fisica/Stamina. Teste Sprint com Mage real verificou LocomotionRate=1.4, evento de magia concluido em movimento e movimento durante Hit. Casos de dash cheio/vazio/diagonal/cooldown/retorno e colisao foram incluidos. Relatorios XML anexados. Nao equivale a teste de teclado/mouse fisicos ou aprovacao visual do game feel.

Como aplicar: parar Play, aguardar compilacao completa, abrir UnderdogSurvival, executar Update Player Locomotion, confirmar backup e iniciar nova sessao de Play. Valores novos de PlayerMovement inicializam automaticamente; conferir Inspector conforme Docs/PlayerMovement.md. Nao executar Prepare Survival Game. Menus Check Player Movement e Check Match Start permitem repetir verificacoes de runtime/inicio. Nenhum logger automatico foi mantido (PlayerInputProbe removido).

Pendentes manuais: WASD/mira fisicos com janela Game em foco; validar se o relato de falta de controle persiste apos a religacao do inicio; corrida em todas as direcoes, recuperacao20, resposta/frenagem, tres habilidades, colisao/dash em grupos e props reais, cinco ondas completas/morte/reinicio. Se reaparecer bloqueio, registrar estado Inicio/Jogando/Derrota e componentes habilitados antes de atribuir uma causa. Camera, ondas, inimigos, dano/vida e layout HUD foram preservados; so o ciclo de ligacao dos botoes do HUD mudou. Alteracoes locais de cena/prefabs/presets do usuario ficam fora dos commits desta etapa. Nenhum push/merge.

## CP8 concluido — expansao de combate e buffs em runtime (2026-11-15)

Usuario confirmou funcionamento da etapa anterior. O novo prompt dizia que IA coletiva/Stamina nao existiam; a inspecao dos arquivos confirmou ambas implementadas. Foram preservadas, sem buffs de Stamina e sem modificar o dash do jogador. Baseline: 4f65714. Assets locais modificados pelo usuario (cena, tres inimigos, Player.controller e pastas de presets/backups) permanecem fora dos commits. Durante esta etapa tambem apareceram exclusoes locais de ArenaTeste_backup e SampleScene 1 (+ metas); nao restaurar nem incluir no commit de codigo.

Implementado:
- CombatDisplacement calcula impulso amortecido sem Update/Move proprio; EnemyAI e PlayerMovement continuam donos do CharacterController. Inimigos limitam o passo ao NavMesh, sincronizam nextPosition e retomam IA apos empurrao.
- EnemySpecialAttack: Warrior pesado 25/alcance2.8/preparo0.9/cooldown6/empurrao; Rogue aviso0.65/dash0.25/vel13/dano15/cooldown5/recuperacao0.4/range3-8, direcao travada, aviso vermelho, dano unico, coordenação de inicio e cancelamento por morte/empurrao; Mage leque de tres projeteis/preparo0.65/cooldown7 em movimento. Especiais entram nas ondas3/4/5 respectivamente; spawn/order/counts intactos. Parametros configuraveis por componente.
- Mage pode navegar/recuar/disparar com rotacao separada. Runtime preparado para camada Upper Body; alternativa temporaria de corpo inteiro ate configurar os Controllers no CP9.
- PlayerCombat: explosao50, impulso radial12 por0.3s, deduplicacao/LOS existentes preservados, feedback de particulas reforcado. Migra somente dano padrao antigo35 em runtime; valores personalizados preservados. Projectile foi inspecionado e reutilizado sem troca de sistema.
- PlayerBuffs: seis definicoes serializaveis, tres ofertas unicas aleatorias, aplicacao unica, acumulos calculados sobre bases capturadas uma vez, limites no Inspector e resistencia em Vida. Vitalidade usa PlayerHp existente. BuffSelectionUI acrescenta somente painel de escolha e aviso da melhoria aplicada (botoes/1-3), sem redesenhar HUD.
- WaveManager acrescenta EscolhendoBuff apos ondas1-4, suspende comandos/spawns ate selecao valida, rejeita repeticao, cancela ao morrer e vence apos5. Composicao e regras de spawn continuam existentes.

Validacao: compilacao de runtime/Editor PASS (dois avisos anteriores). Runner Unity2022.3 PlayMode em copia isolada: 2/2 PASS, suites CombatExpansionChecks e BuffChecks. Cobertura: pesado/empurrao; aviso/direcao fixa/esquiva/dano unico/morte do Rogue; Mage preparando em movimento e tres tiros em leque; empurrao contra parede/NavMesh e retomada; explosao50 em dois alvos com colliders duplicados e impulso radial; seis buffs/acumulos/limites/resistencia/vitalidade; 40 spawns, quatro escolhas, nenhum spawn sem selecao, indices invalidos/duplo clique, morte durante escolha e quinta onda/vitoria. A primeira falha de Vitalidade era fixture com aceitaDano=false antes de Iniciar; corrigido somente o teste para simular dano valido.

Arquivos: CombatDisplacement.cs, EnemySpecialAttack.cs, EnemyAI.cs, PlayerMovement.cs (apenas impulso adicional), PlayerCombat.cs, Vida.cs, WaveManager.cs, PlayerBuffs.cs, BuffSelectionUI.cs, CombatExpansionChecks.cs e BuffChecks.cs (+ novos metas), Docs/Validation/CombatExpansion-Runtime.xml e este checkpoint.

Falta CP9: validar ferramenta segura CombatExpansionSetup, Controllers/mask/animacoes reais e assets de aviso/rajada na copia, teste de reinicio/restauracao de atributos, regressao anterior, instrucoes e verificacao de integridade. Nenhum asset de cena/prefab/Animator do usuario foi configurado automaticamente. Play Mode automatizado nao significa aprovacao visual ou teste humano de balanceamento.

## CP9 concluido — configuracao segura, animacoes reais e regressao (2026-11-15)

CP8 registrado no commit local 26b039f. Retomada consultou checkpoint, status, relatórios e hashes; nao refez runtime concluido nem incorporou arquivos locais do usuario. Novo guia completo: Docs/CombatExpansion.md.

Implementado e integrado:
- CombatExpansionSetup.cs (+ meta): Tools > Underdog > Prepare Combat Expansion identifica cena/3 prefabs reais no WaveManager, valida Unity2022.3/Edit Mode/arquivos salvos, apresenta confirmacao com os caminhos, cria backups antes de escrever e configura via APIs do Editor. Copia Controllers, preserva GUIDs/fileIDs/componentes existentes, cria material URP vermelho transparente e prefab roxo da rajada; salva a cena atual com PlayerBuffs e explosao50. Rollback restaura cena/prefabs em falha; originais KayKit e Controllers anteriores permanecem intactos. Cada execucao cria pasta propria e reutiliza componentes/camadas sem duplicacao.
- Mage: mascara baseada no spine do rig real, Attack/Special na Upper Body, pernas em Blend Tree com Idle_A/Running_A e Walking_Backwards/Running_Strafe_Left/Right. Rogue: Dodge_Forward ajustado a duracao da investida. Warrior/Rogue reutilizam Melee_1H_Attack_Chop e Mage Ranged_Magic_Shoot para preparo com SpecialRate; copias dos especiais removem eventos basicos, preservando evento unico nos ataques normais. Clips descobertos via AssetDatabase.
- Ajustes confirmados na integracao: apos impulso/investida limpa caminho antigo e agenda nova decisao (teste de retomada real incluido); aviso pesado usa cone coerente com o alcance; atraso/variacao dos especiais configuraveis; recalculo de buffs nao piora cooldown/resistencia personalizados anteriores; clique da escolha nao dispara magia no mesmo frame em que PlayerCombat e reativado.
- CombatExpansionSetupChecks.cs (+ meta) oferece verificacao repetivel dos assets preparados. SurvivalChecks atualizado para dano50, quatro escolhas obrigatorias e dano fatal de teste suficiente apos buffs; SurvivalGameSetup atualiza somente dano inicial50 para futuras geracoes.

Validacoes concluidas no Native Test Runner Unity2022.3.62f3 em Temp/StartSceneVerification (copia isolada da cena e assets reais, sem Pipeline):
- PlayMode 8/8 PASS: CombatCoreTests (especiais/empurrao/buffs/cinco ondas), CombatSceneTests (Mage real movendo pernas e disparando por Animation Event; Rogue real com aviso/Dodge; reinicio real sem buffs/atributos residuais) e regressao existente de grupos25/movimentacao/sobrevivencia. Relatorio Docs/Validation/CombatExpansion-PlayMode.xml.
- EditMode 3/3 PASS: CombatAssetTests (backups, GUID/fileID, Controllers originais e preservacao de camera/HUD/ondas/dash; mascara/animacoes/eventos/materiais) e PlayerCombat/CombatFeedback anteriores. Relatorio Docs/Validation/CombatExpansion-EditMode.xml.
- Falhas intermediarias de regressao eram expectativas/fixtures antigas: Vitalidade com aceitaDano=false, comparacao float exata, dano100 insuficiente apos buffs e cena de teste carregada sem buildIndex. Corrigidos somente os testes correspondentes; reinicio real via WaveManager.Reiniciar foi comprovado com Build Settings da copia, sem mudar os do projeto principal.
- Hashes SHA256 dos cinco assets locais protegidos iguais ao inicio (cena, Melee/Fast/Ranged e Player.controller). Sem alteracao em SampleScene, KayKit, pacotes, ProjectSettings, camera, ArenaNavigation, SurvivalHUD ou Projectile. Compilacao runtime/Editor passou; somente avisos preexistentes NavigationStatic e ItemEffect. git diff --check aprovado.

Estado de entrega: runtime, configurador seguro, suites e documentacao concluidos. Nao refazer CP8/CP9. Os assets da partida principal NAO foram configurados automaticamente; executar fora de Play: salvar trabalho pendente > Prepare Combat Expansion > conferir/confirmar caminhos > Check Combat Expansion Setup (Edit Mode) > nova sessao Play. O configurador salva a cena atual apos backup e confirmacao; nao regera a arena nem requer rebake do NavMesh. Todos os valores de especiais, empurrao e catalogo/limites de buffs estao no Inspector. Detalhes e arquivos no guia.

Pendente exclusivamente na Unity interativa: aplicar na cena principal; avaliar animacoes/visibilidade/transparencia/efeitos e UI em Game, teclado/mouse fisicos, colisao nos props reais, balanceamento das quatro escolhas/especiais, cinco ondas jogadas por uma pessoa, derrota/reinicio e Profiler com 15-25. Testes automatizados usam fixtures e copia real, nao comprovam sensacao visual ou desempenho de hardware. Alteracoes/exclusoes locais do usuario continuam fora dos commits. Nenhum push/merge.
